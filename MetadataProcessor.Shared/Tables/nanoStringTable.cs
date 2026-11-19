// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Original work from Oleg Rakhmatulin.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Encapsulates logic for storing strings list and writing this
    /// list into target assembly in .NET nanoFramework format.
    /// </summary>
    public sealed class nanoStringTable : InanoTable
    {
        /// <summary>
        /// Default implementation of <see cref="ICustomStringSorter"/> interface.
        /// Do nothing and just returns original sequence of string literals.
        /// </summary>
        private sealed class EmptyStringSorter : ICustomStringSorter
        {
            /// <inheritdoc/>
            public IEnumerable<string> Sort(
                ICollection<string> strings)
            {
                return strings;
            }
        }

        /// <summary>
        /// Maps for each unique string and related identifier (offset in strings table).
        /// </summary>
        private readonly Dictionary<string, ushort> _idsByStrings =
            new Dictionary<string, ushort>(StringComparer.Ordinal);

        /// <summary>
        /// Concrete implementation of string literals sorting algorithm (used by UTs).
        /// </summary>
        private readonly ICustomStringSorter _stringSorter;

        /// <summary>
        /// Last available string identifier.
        /// </summary>
        private ushort _lastAvailableId;

        private readonly HashSet<string> _userStrings = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _assemblyNames = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _knownStrings = new HashSet<string>(StringComparer.Ordinal);
        private byte[] _frozenData;
        private bool _collecting;

        internal bool IsFrozen => _frozenData != null;

        /// <summary>
        /// Assembly tables context - contains all tables used for building target assembly.
        /// </summary>
        private readonly nanoTablesContext _context;

        public NanoClrTable TableIndex => NanoClrTable.TBL_Strings;

        /// <summary>
        /// Creates new instance of <see cref="nanoStringTable"/> object.
        /// </summary>
        public nanoStringTable(
            nanoTablesContext context,
            ICustomStringSorter stringSorter = null)
        {
            _context = context;
            GetOrCreateStringId(string.Empty); // First item in string table always empty string
            _stringSorter = stringSorter ?? new EmptyStringSorter();
        }

        /// <summary>
        /// Gets existing or creates new string reference identifier related to passed string value.
        /// </summary>
        /// <remarks>
        /// Identifier is offset in strings table or just number from table of pre-defined constants.
        /// </remarks>
        /// <param name="value">String value for obtaining identifier.</param>
        /// <param name="useConstantsTable">
        /// If <c>true</c>, shared string constants may be used for metadata and IL literals.
        /// </param>
        /// <returns>Existing identifier if string already in table or new one.</returns>
        public ushort GetOrCreateStringId(
            string value,
            bool useConstantsTable = true)
        {
            if (IsFrozen && !_knownStrings.Contains(value))
            {
                throw new InvalidOperationException($"String '{value}' was not collected before freezing assembly '{_context.AssemblyDefinition.Name}'.");
            }

            _knownStrings.Add(value);
            ushort id;
            if (useConstantsTable &&
                !_assemblyNames.Contains(value) &&
                nanoStringsConstants.TryGetStringIndex(value, out id))
            {
                return id;
            }
            if (!_idsByStrings.TryGetValue(value, out id))
            {
                if (IsFrozen)
                {
                    throw new InvalidOperationException($"String '{value}' was not collected before freezing assembly '{_context.AssemblyDefinition.Name}'.");
                }

                if (_collecting)
                {
                    _idsByStrings.Add(value, 0);
                    return 0;
                }

                var length = Encoding.UTF8.GetByteCount(value) + 1;

                // sanity check for ID overflow
                if (_lastAvailableId + length > nanoStringsConstants.FirstId)
                {
                    throw new InvalidOperationException($"String table overflow in assembly '{_context.AssemblyDefinition.Name}'. Can't use so many strings.");
                }

                id = _lastAvailableId;
                _idsByStrings.Add(value, id);
                _lastAvailableId += (ushort)(length);
            }
            return id;
        }

        /// <summary>
        /// Gets existing or creates new string reference identifier for an assembly name (of this assembly or of an AssemblyRef).
        /// </summary>
        /// <remarks>
        /// Assembly names are always stored as plain strings in the PE, never as an index in the pre-defined constants table
        /// (mscorlib included), so tools can read them without a copy of that table.
        /// </remarks>
        /// <param name="name">Assembly name.</param>
        /// <returns>Existing identifier if string already in table or new one.</returns>
        public ushort GetOrCreateAssemblyNameId(string name)
        {
            _assemblyNames.Add(name);
            return GetOrCreateStringId(name, false);
        }

        internal ushort GetOrCreateStringLiteralId(string value)
        {
            _userStrings.Add(value);
            return GetOrCreateStringId(value);
        }

        internal bool IsUserString(string value) => _userStrings.Contains(value);

        internal IEnumerable<KeyValuePair<string, ushort>> GetUserStrings()
        {
            return _userStrings.Select(value =>
                new KeyValuePair<string, ushort>(value, GetOrCreateStringId(value)));
        }

        internal void BeginCollection()
        {
            if (IsFrozen)
            {
                throw new InvalidOperationException("The string table is already frozen.");
            }

            _idsByStrings.Clear();
            _userStrings.Clear();
            _assemblyNames.Clear();
            _knownStrings.Clear();
            _lastAvailableId = 0;
            _collecting = true;
            GetOrCreateStringId(string.Empty, false);
        }

        internal void Freeze()
        {
            if (IsFrozen)
            {
                throw new InvalidOperationException("The string table is already frozen.");
            }

            // Offsets are assigned before signatures and resource records; see CLAUDE.md "Frozen string layout".
            var strings = _idsByStrings.Keys.Where(s => s.Length > 0)
                .OrderByDescending(s => Encoding.UTF8.GetByteCount(s))
                .ThenBy(s => s, StringComparer.Ordinal)
                .ToList();
            var roots = new List<KeyValuePair<byte[], ushort>>();
            _idsByStrings.Clear();
            _idsByStrings.Add(string.Empty, 0);

            using (var stream = new MemoryStream())
            {
                stream.WriteByte(0);
                foreach (string value in strings)
                {
                    byte[] data = Encoding.UTF8.GetBytes(value);
                    var root = roots.FirstOrDefault(r => r.Key.Length >= data.Length &&
                        r.Key.Skip(r.Key.Length - data.Length).SequenceEqual(data));
                    if (root.Key != null)
                    {
                        _idsByStrings.Add(value, (ushort)(root.Value + root.Key.Length - data.Length));
                        continue;
                    }

                    if (stream.Length + data.Length + 1 > nanoStringsConstants.FirstId)
                    {
                        throw new InvalidOperationException($"String table overflow in assembly '{_context.AssemblyDefinition.Name}'.");
                    }

                    ushort offset = (ushort)stream.Length;
                    _idsByStrings.Add(value, offset);
                    roots.Add(new KeyValuePair<byte[], ushort>(data, offset));
                    stream.Write(data, 0, data.Length);
                    stream.WriteByte(0);
                }

                _frozenData = stream.ToArray();
                _collecting = false;
            }
        }

        /// <summary>
        /// Try to get a string value from the table providing the reference identifier.
        /// </summary>
        /// <param name="id">Existing identifier in table.</param>
        /// <returns>The string value or a null if the identifier doesn't exist.</returns>
        public string TryGetString(ushort id)
        {
            // try to get string from id table
            var output = _idsByStrings.FirstOrDefault(s => s.Value == id).Key;

            if (output == null)
            {
                // try to find string in string constant table
                output = nanoStringsConstants.TryGetString(id);
            }

            return output;
        }

        /// <inheritdoc/>
        public void Write(
            nanoBinaryWriter writer)
        {
            if (_collecting)
            {
                throw new InvalidOperationException("The string table must be frozen before writing.");
            }

            if (IsFrozen)
            {
                writer.WriteBytes(_frozenData);
                return;
            }

            foreach (var item in _idsByStrings
                .OrderBy(item => item.Value)
                .Select(item => item.Key))
            {
                writer.WriteString(item);
            }
        }

        /// <summary>
        /// Adds all string constants from <paramref name="fakeStringTable"/> table into this one.
        /// </summary>
        /// <param name="fakeStringTable">Additional string table for merging with this one.</param>
        internal void MergeValues(
            nanoStringTable fakeStringTable)
        {
            foreach (var item in _stringSorter.Sort(fakeStringTable._idsByStrings.Keys))
            {
                GetOrCreateStringId(item, false);
                if (fakeStringTable.IsUserString(item))
                {
                    _userStrings.Add(item);
                }
            }
        }

        public Dictionary<string, ushort> GetItems()
        {
            return _idsByStrings;
        }
    }
}
