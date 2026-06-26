// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using nanoFramework.Tools.MetadataProcessor.Core;
using nanoFramework.Tools.Utilities;

namespace nanoFramework.Tools.MetadataProcessor.MsBuildTask
{
    [Description("MetaDataProcessorTaskEntry")]
    public class MetaDataProcessorTask : Task
    {

        #region public properties for the task

        /// <summary>
        /// Array of nanoFramework assemblies to be passed to MetaDataProcessor in -loadHints switch 
        /// </summary>
        public ITaskItem[] LoadHints { get; set; }

        public ITaskItem[] IgnoreAssembly { get; set; }

        public ITaskItem[] Load { get; set; }

        public ITaskItem[] LoadDatabase { get; set; }

        public string LoadStrings { get; set; }

        public ITaskItem[] ImportResources { get; set; }

        public string Parse { get; set; }

        public string GenerateStringsTable { get; set; }

        public string Compile { get; set; }

        public bool Verbose { get; set; }

        public bool VerboseMinimize { get; set; }

        public bool NoByteCode { get; set; }

        public bool NoAttributes { get; set; }

        public ITaskItem[] CreateDatabase { get; set; }

        /// <summary>
        /// Parameter to enable stubs generation step.
        /// </summary>
        public bool GenerateStubs { get; set; } = false;

        public string GenerateSkeletonFile { get; set; }

        public string GenerateSkeletonName { get; set; }

        public string GenerateSkeletonProject { get; set; }

        public string GenerateDependency { get; set; }

        public string CreateDatabaseFile { get; set; }

        /// <summary>
        /// Option to generate skeleton project without Interop support.
        /// This is required to generate Core Libraries.
        /// Default is false, meaning that Interop support will be used.
        /// </summary>
        public bool SkeletonWithoutInterop { get; set; } = false;

        public bool Resolve { get; set; }

        public string SaveStrings { get; set; }

        public bool DumpMetadata { get; set; }

        public string DumpFile { get; set; }

        public string DumpExports { get; set; }

        /// <summary>
        /// Flag to set when compiling a Core Library.
        /// </summary>
        public bool IsCoreLibrary { get; set; } = false;

        private readonly List<ITaskItem> _filesWritten = new List<ITaskItem>();

        [Output]
        public ITaskItem[] FilesWritten { get { return _filesWritten.ToArray(); } }

        [Output]
        public ITaskItem NativeChecksum { get { return new TaskItem(_nativeChecksum); } }

        #endregion

        #region internal fields for MetadataProcessor

        private AssemblyDefinition _assemblyDefinition;
        private nanoAssemblyBuilder _assemblyBuilder;
        private readonly IDictionary<string, string> _loadHints =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private string _nativeChecksum = "";

        #endregion


        public override bool Execute()
        {
            System.Reflection.Assembly taskAssembly = typeof(MetaDataProcessorTask).Assembly;
            object[] infoVerAttrs = taskAssembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false);
            string taskVersion = infoVerAttrs.Length > 0
                ? ((System.Reflection.AssemblyInformationalVersionAttribute)infoVerAttrs[0]).InformationalVersion
                : taskAssembly.GetName().Version?.ToString() ?? "unknown";

            // report to VS output window what step the build is 
            Log.LogCommandLine(MessageImportance.Normal, $"Starting nanoFramework MetadataProcessor (v{taskVersion}).");

            /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // developer note: to debug this task set an environment variable like this:
            // set NFBUILD_TASKS_DEBUG=1
            // this will cause the execution to pause bellow so a debugger can be attached
            DebuggerHelper.WaitForDebuggerIfEnabled(TasksConstants.BuildTaskDebugVar, Log);
            /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            try
            {
                // execution of the metadata processor have to be carried in the appropriate order
                // failing to do so will most likely cause the task to fail

                // load hints for referenced assemblies
                if (LoadHints != null &&
                    LoadHints.Any())
                {
                    if (Verbose) Log.LogCommandLine(MessageImportance.Normal, "Processing load hints");

                    foreach (var hint in LoadHints)
                    {
                        var assemblyName = Path.GetFileNameWithoutExtension(hint.GetMetadata("FullPath"));
                        var assemblyPath = hint.GetMetadata("FullPath");

                        _loadHints[assemblyName] = assemblyPath;

                        if (Verbose) Log.LogCommandLine(MessageImportance.Normal, $"Adding load hint: {assemblyName} @ '{assemblyPath}'");
                    }
                }

                // Analyses a .NET assembly
                if (!string.IsNullOrEmpty(Parse))
                {
                    if (Verbose) Log.LogCommandLine(MessageImportance.Normal, $"Analysing .NET assembly {Path.GetFileNameWithoutExtension(Parse)}");

                    ExecuteParse(Parse);
                }

                // compiles an assembly into nanoCLR format
                if (!string.IsNullOrEmpty(Compile))
                {
                    // sanity check for missing parse
                    if (string.IsNullOrEmpty(Parse))
                    {
                        // can't compile without analysing first
                        throw new ArgumentException("Can't compile without first analysing a .NET Assembly. Check the targets file for a missing option invoking MetadataProcessor Task.");
                    }
                    else
                    {
                        if (Verbose) Log.LogCommandLine(MessageImportance.Normal, $"Compiling {Path.GetFileNameWithoutExtension(Compile)} into nanoCLR format.");

                        ExecuteCompile(Compile);
                    }
                }

                // generate skeleton files with stubs to add native code for an assembly
                if (GenerateStubs)
                {
                    if (string.IsNullOrEmpty(GenerateSkeletonFile))
                    {
                        // can't generate skeleton without GenerateSkeletonFile parameter
                        throw new ArgumentException("Can't generate skeleton project without 'GenerateSkeletonFile'. Check the targets file for a missing parameter when invoking MetadataProcessor Task.");
                    }

                    if (string.IsNullOrEmpty(GenerateSkeletonProject))
                    {
                        // can't generate skeleton without GenerateSkeletonProject parameter
                        throw new ArgumentException("Can't generate skeleton project without 'GenerateSkeletonProject'. Check the targets file for a missing parameter when invoking MetadataProcessor Task.");
                    }

                    if (string.IsNullOrEmpty(GenerateSkeletonName))
                    {
                        // can't generate skeleton without GenerateSkeletonName parameter
                        throw new ArgumentException("Can't generate skeleton project without 'GenerateSkeletonName'. Check the targets file for a missing parameter when invoking MetadataProcessor Task.");
                    }

                    // sanity check for missing compile (therefore parse too)
                    if (string.IsNullOrEmpty(Compile))
                    {
                        // can't generate skeleton without compiling first
                        throw new ArgumentException("Can't generate skeleton project without first compiling the .NET Assembly. Check the targets file for a missing option invoking MetadataProcessor Task.");
                    }
                    else
                    {
                        if (Verbose) Log.LogCommandLine(MessageImportance.Normal, $"Generating skeleton '{GenerateSkeletonName}' for {GenerateSkeletonProject} \r\nPlacing files @ '{GenerateSkeletonFile}'");

                        ExecuteGenerateSkeleton(
                            GenerateSkeletonFile,
                            GenerateSkeletonName,
                            GenerateSkeletonProject,
                            SkeletonWithoutInterop);
                    }
                }

                RecordFilesWritten();
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, true);
            }
            finally
            {
                // need to dispose the AssemblyDefinition before leaving because Mono.Cecil assembly loading and resolution
                // operations leave the assembly file locked in the AppDomain preventing it from being open on subsequent Tasks
                // see https://github.com/nanoframework/Home/issues/553
                if (_assemblyDefinition != null)
                {
                    _assemblyDefinition.Dispose();
                }
            }

            // if we've logged any errors that's because there were errors (WOW!)
            return !Log.HasLoggedErrors;
        }

        private void RecordFileWritten(
            string file)
        {
            if (!string.IsNullOrEmpty(file))
            {
                if (File.Exists(file))
                {
                    _filesWritten.Add(new TaskItem(file));
                }
            }
        }

        private void RecordFilesWritten()
        {
            RecordFileWritten(SaveStrings);
            RecordFileWritten(GenerateStringsTable);
            RecordFileWritten(DumpFile);
            RecordFileWritten(DumpExports);
            RecordFileWritten(Compile);
            RecordFileWritten(Path.ChangeExtension(Compile, "pdbx"));
            RecordFileWritten(CreateDatabaseFile);
            RecordFileWritten(GenerateDependency);
        }

        #region Metadata Processor helper methods

        private void ExecuteParse(
            string fileName)
        {
            try
            {
                if (Verbose) Log.LogCommandLine(MessageImportance.Normal, "Parsing assembly..");

                _assemblyDefinition = AssemblyDefinition.ReadAssembly(fileName,
                    new ReaderParameters { AssemblyResolver = new LoadHintsAssemblyResolver(_loadHints) });

                Log.LogMessage(MessageImportance.Low, $"[MDP] Assembly parsed: {_assemblyDefinition.FullName}");
                Log.LogMessage(MessageImportance.Low, $"[MDP]   Types       : {_assemblyDefinition.MainModule.Types.Count}");
                Log.LogMessage(MessageImportance.Low, $"[MDP]   References  : {_assemblyDefinition.MainModule.AssemblyReferences.Count}");
            }
            catch (Exception)
            {
                Log.LogError($"Unable to parse input assembly file '{fileName}' - check if path and file exists");
            }
        }

        private void ExecuteCompile(
            string fileName)
        {
            FileStream logOutputStream = null;
            StreamWriter logWriter = null;
            string logFile = "";
            TextWriter originalConsoleOut = null;

            try
            {
                if (Verbose)
                {
                    // Save original
                    originalConsoleOut = Console.Out;

                    logFile = Path.ChangeExtension(fileName, "log.txt");
                    logOutputStream = new FileStream(logFile, FileMode.OpenOrCreate, FileAccess.Write);
                    logWriter = new StreamWriter(logOutputStream);
                    Console.SetOut(logWriter);
                }
            }
            catch
            {
                Log.LogError($"Unable to create log file '{logFile}'.");
            }

            try
            {
                // compile assembly (1st pass)
                if (Verbose)
                {
                    var message = "[MDP] Compiling assembly...";
                    Log.LogCommandLine(MessageImportance.Normal, message);
                    Console.WriteLine(message);

                    var timeMessage = $"[MDP] Started at: {DateTime.Now.ToString("HH:mm:ss.fff")}";
                    Log.LogMessage(timeMessage);
                    Console.WriteLine(timeMessage);
                }

                _assemblyBuilder = new nanoAssemblyBuilder(
                    _assemblyDefinition,
                    Verbose,
                    IsCoreLibrary);

                Log.LogMessage(MessageImportance.Low, "[MDP] Tables context built:");
                LogTablesDetailed();

                LogExcludedTypesDetailed();
                LogNativeCrcDetailed();

                using (var stream = File.Open(Path.ChangeExtension(fileName, "tmp"), FileMode.Create, FileAccess.ReadWrite))
                using (var writer = new BinaryWriter(stream))
                {
                    DateTime startTime = DateTime.Now;

                    _assemblyBuilder.Write(GetBinaryWriter(writer));

                    if (Verbose)
                    {
                        TimeSpan elapsed = DateTime.Now - startTime;
                        var message = $"[MDP] Writting assembly tables took {elapsed.Minutes:D2}:{elapsed.Seconds:D2}.{elapsed.Milliseconds:D3}";
                        Log.LogMessage(message);
                        Console.WriteLine(message);
                    }

                    Log.LogMessage(MessageImportance.Low, $"[MDP] 1st pass PE size: {stream.Length} bytes");
                }
            }
            catch (Exception)
            {
                Log.LogError($"Unable to compile output assembly file '{fileName}' - check parse command results");

                if (Verbose)
                {
                    logWriter?.Close();
                    logOutputStream?.Close();
                }

                throw;
            }

            try
            {
                // OK to delete tmp PE file
                File.Delete(Path.ChangeExtension(fileName, "tmp"));

                // minimize (has to be called after the 1st compile pass)
                if (Verbose)
                {
                    var message = "Minimizing assembly..";
                    Log.LogCommandLine(MessageImportance.Normal, message);
                    Console.WriteLine(message);
                }

                DateTime startTime = DateTime.Now;

                _assemblyBuilder.Minimize();

                if (Verbose)
                {
                    TimeSpan elapsed = DateTime.Now - startTime;
                    var message = $"[MDP] Minimizing assembly took {elapsed.Minutes:D2}:{elapsed.Seconds:D2}.{elapsed.Milliseconds:D3}";
                    Log.LogMessage(message);
                    Console.WriteLine(message);
                }

                Log.LogMessage(MessageImportance.Low, "[MDP] Post-minimize tables:");
                LogTablesDetailed();

                // compile assembly (2nd pass after minimize)
                if (Verbose)
                {
                    var message = "[MDP] Recompiling assembly..";
                    Log.LogCommandLine(MessageImportance.Normal, message);
                    Console.WriteLine(message);
                }

                using (var stream = File.Open(fileName, FileMode.Create, FileAccess.ReadWrite))
                using (var writer = new BinaryWriter(stream))
                {
                    startTime = DateTime.Now;

                    _assemblyBuilder.Write(GetBinaryWriter(writer));

                    if (Verbose)
                    {
                        TimeSpan elapsed = DateTime.Now - startTime;
                        var message = $"[MDP] Writting assembly tables took {elapsed.Minutes:D2}:{elapsed.Seconds:D2}.{elapsed.Milliseconds:D3}";
                        Log.LogMessage(message);
                        Console.WriteLine(message);
                    }

                    Log.LogMessage(MessageImportance.Low, $"[MDP] Final PE size: {stream.Length} bytes -> '{fileName}'");
                }

                LogAssemblyDefinitionCrcDetailed();

                startTime = DateTime.Now;

                // output PDBX
                _assemblyBuilder.Write(Path.ChangeExtension(fileName, "pdbx"));

                if (Verbose)
                {
                    TimeSpan elapsed = DateTime.Now - startTime;
                    var message = $"[MDP] Outputting PDBX file took {elapsed.Minutes:D2}:{elapsed.Seconds:D2}.{elapsed.Milliseconds:D3}";
                    Log.LogMessage(message);
                    Console.WriteLine(message);
                }

                Log.LogMessage(MessageImportance.Low, $"[MDP] PDBX written -> '{Path.ChangeExtension(fileName, "pdbx")}'");

                // output assembly metadata
                if (DumpMetadata)
                {
                    if (Verbose)
                    {
                        var message = "[MDP] Dumping assembly metadata..";
                        Log.LogCommandLine(MessageImportance.Normal, message);
                        Console.WriteLine(message);
                    }

                    DumpFile = Path.ChangeExtension(fileName, "dump.txt");

                    startTime = DateTime.Now;

                    nanoDumperGenerator dumper = new nanoDumperGenerator(
                        _assemblyBuilder.TablesContext,
                        DumpFile);
                    dumper.DumpAll();

                    if (Verbose)
                    {
                        TimeSpan elapsed = DateTime.Now - startTime;
                        var message = $"[MDP] Dumping metadata took {elapsed.Minutes:D2}:{elapsed.Seconds:D2}.{elapsed.Milliseconds:D3}";
                        Log.LogMessage(message);
                        Console.WriteLine(message);
                    }
                }

                // set environment variable with assembly native checksum
                Environment.SetEnvironmentVariable("AssemblyNativeChecksum", _assemblyBuilder.GetNativeChecksum(), EnvironmentVariableTarget.Process);

                // store assembly native checksum
                _nativeChecksum = _assemblyBuilder.GetNativeChecksum();

                Log.LogMessage(MessageImportance.Low, $"[MDP] Native checksum: {_nativeChecksum}");
            }
            catch (ArgumentException ex)
            {
                Log.LogError($"Exception minimizing assembly: {ex.Message}");
            }
            catch (Exception)
            {
                Log.LogError($"Exception minimizing assembly");
                throw;
            }
            finally
            {
                if (Verbose)
                {
                    var endMessage = $"[MDP] Completed at: {DateTime.Now.ToString("HH:mm:ss.fff")}";
                    Log.LogMessage(endMessage);
                    Console.WriteLine(endMessage);

                    logWriter?.Close();
                    logOutputStream?.Close();

                    if (originalConsoleOut != null)
                    {
                        // Restore original
                        Console.SetOut(originalConsoleOut);
                    }
                }
            }
        }

        private void LogTablesDetailed()
        {
            nanoTablesContext ctx = _assemblyBuilder.TablesContext;

            // Type definitions
            List<TypeDefinition> typeDefs = ctx.TypeDefinitionTable.Items.ToList();
            Log.LogMessage(MessageImportance.Low, $"[MDP]   Type definitions    : {typeDefs.Count}");
            foreach (TypeDefinition t in typeDefs)
            {
                Log.LogMessage(MessageImportance.Low, $"[MDP]     {t.FullName}");
            }

            // Method definitions
            List<MethodDefinition> methodDefs = ctx.MethodDefinitionTable.Items.ToList();
            Log.LogMessage(MessageImportance.Low, $"[MDP]   Method definitions  : {methodDefs.Count}");
            foreach (MethodDefinition m in methodDefs)
            {
                Log.LogMessage(MessageImportance.Low, $"[MDP]     {m.DeclaringType.FullName}::{m.Name}");
            }

            // Field definitions
            List<FieldDefinition> fieldDefs = ctx.FieldsTable.Items.ToList();
            Log.LogMessage(MessageImportance.Low, $"[MDP]   Field definitions   : {fieldDefs.Count}");
            foreach (FieldDefinition f in fieldDefs)
            {
                Log.LogMessage(MessageImportance.Low, $"[MDP]     {f.DeclaringType.FullName}::{f.Name}");
            }

            // Assembly references
            List<AssemblyNameReference> asmRefs = ctx.AssemblyReferenceTable.Items.ToList();
            Log.LogMessage(MessageImportance.Low, $"[MDP]   Assembly references : {asmRefs.Count}");
            foreach (AssemblyNameReference a in asmRefs)
            {
                Log.LogMessage(MessageImportance.Low, $"[MDP]     {a.FullName}");
            }

            // Type references
            List<TypeReference> typeRefs = ctx.TypeReferencesTable.Items.ToList();
            Log.LogMessage(MessageImportance.Low, $"[MDP]   Type references     : {typeRefs.Count}");
            foreach (TypeReference t in typeRefs)
            {
                Log.LogMessage(MessageImportance.Low, $"[MDP]     {t.FullName}");
            }

            // Member references
            List<MemberReference> memberRefs = ctx.MemberReferencesTable.Items.ToList();
            Log.LogMessage(MessageImportance.Low, $"[MDP]   Member references   : {memberRefs.Count}");
            foreach (MemberReference m in memberRefs)
            {
                Log.LogMessage(MessageImportance.Low, $"[MDP]     {m.DeclaringType.FullName}::{m.Name}");
            }
        }

        private void LogAssemblyDefinitionCrcDetailed()
        {
            Log.LogMessage(MessageImportance.Low, $"[MDP] Assembly PE CRC inputs:");
            Log.LogMessage(MessageImportance.Low, $"[MDP]   Assembly version          : {_assemblyBuilder.LastAssemblyVersion}");
            Log.LogMessage(MessageImportance.Low, $"[MDP]   Native methods checksum   : 0x{_assemblyBuilder.LastNativeMethodsChecksum:X8}");
            Log.LogMessage(MessageImportance.Low, $"[MDP]   Total PE size             : {_assemblyBuilder.LastTotalSize} bytes");
            Log.LogMessage(MessageImportance.Low, $"[MDP]   Header region             : offset 0x0000, length {_assemblyBuilder.LastHeaderSize} bytes");
            Log.LogMessage(MessageImportance.Low, $"[MDP]   Body region               : offset 0x{_assemblyBuilder.LastHeaderSize:X4}, length {_assemblyBuilder.LastBodySize} bytes");
            Log.LogMessage(MessageImportance.Low, $"[MDP]   CRC32 body                : 0x{_assemblyBuilder.LastAssemblyCrc32:X8}");
            Log.LogMessage(MessageImportance.Low, $"[MDP]   CRC32 header              : 0x{_assemblyBuilder.LastHeaderCrc32:X8}");
        }

        private void LogNativeCrcDetailed()
        {
            IReadOnlyList<string> entries = _assemblyBuilder.TablesContext.NativeMethodsCrc.GetCrcLog();

            if (entries.Count == 0)
            {
                Log.LogMessage(MessageImportance.Low, "[MDP] Native CRC: no methods with native implementation found.");
                return;
            }

            Log.LogMessage(MessageImportance.Low, $"[MDP] Native CRC method list ({entries.Count} entries):");
            Log.LogMessage(MessageImportance.Low,  "[MDP]    idx  CRC after     Method signature");

            foreach (string entry in entries)
            {
                Log.LogMessage(MessageImportance.Low, $"[MDP] {entry}");
            }

            Log.LogMessage(MessageImportance.Low, $"[MDP] Native CRC final value: 0x{_assemblyBuilder.TablesContext.NativeMethodsCrc.CurrentCrc:X8}");
        }

        private void LogExcludedTypesDetailed()
        {
            var excludedTypes = nanoTablesContext.ClassNamesToExclude;

            if (excludedTypes == null || excludedTypes.Count == 0)
            {
                return;
            }

            var distinctExcludedTypes = excludedTypes
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToList();

            if (distinctExcludedTypes.Count == 0)
            {
                return;
            }

            Log.LogMessage(MessageImportance.Low, $"[MDP] Types excluded from processing ({distinctExcludedTypes.Count}):");

            foreach (var typeName in distinctExcludedTypes)
            {
                Log.LogMessage(MessageImportance.Low, $"[MDP]   - {typeName}");
            }
        }

        private void ExecuteGenerateSkeleton(
            string file,
            string name,
            string project,
            bool withoutInteropCode)
        {
            try
            {
                if (Verbose) Log.LogCommandLine(MessageImportance.Normal, "Generating skeleton files..");

                var skeletonGenerator = new nanoSkeletonGenerator(
                    _assemblyBuilder.TablesContext,
                    file,
                    name,
                    project,
                    withoutInteropCode,
                    IsCoreLibrary);

                skeletonGenerator.GenerateSkeleton();

                Log.LogMessage(MessageImportance.Low, $"[MDP] Skeleton generated: '{name}' for project '{project}' @ '{file}' (withoutInterop={withoutInteropCode})");
            }
            catch (Exception)
            {
                Log.LogError("Unable to generate skeleton files");

                throw;
            }
        }

        private void ExecuteGenerateDependency(
            string fileName)
        {
            try
            {
                var dependencyGenerator = new nanoDependencyGenerator(
                    _assemblyDefinition,
                    _assemblyBuilder.TablesContext,
                    fileName);

                using (var writer = XmlWriter.Create(fileName))
                {
                    dependencyGenerator.Write(writer);
                }
            }
            catch (Exception)
            {
                Log.LogError($"Unable to generate and write dependency graph for assembly file '{fileName}'");

                throw;
            }
        }

        private nanoBinaryWriter GetBinaryWriter(
            BinaryWriter writer)
        {
            return nanoBinaryWriter.CreateLittleEndianBinaryWriter(writer);
        }

        #endregion

    }
}
