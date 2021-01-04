using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
using System.IO;
using System.Text;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Tables
{
    [TestClass]
    public class nanoReferenceTableBaseTests
    {
        [TestMethod]
        public void ConstructorTest()
        {
            var items = new List<object>() { 1, 2, 3 };

            var comparer = new ObjectComparer();
            var context = TestObjectHelper.GetTestNFAppNanoTablesContext();

            // test
            var iut = new TestNanoReferenceTable(items, comparer, context);

            Assert.IsTrue(comparer.EqualsCallParameters.Any() || comparer.GetHashCodeCallParameters.Any());
        }

        [TestMethod]
        public void ForEachItemsTest()
        {
            var items = new List<object>() { 1, 2, 3 };

            var comparer = new ObjectComparer();
            var context = TestObjectHelper.GetTestNFAppNanoTablesContext();
            var iut = new TestNanoReferenceTable(items, comparer, context);

            // test
            var forEachCalledOnItems = new List<object>();
            iut.ForEachItems((idx, item) =>
            {
                forEachCalledOnItems.Add(item);
                Assert.AreEqual(items.IndexOf(item), (int)idx);
            });

            CollectionAssert.AreEqual(items.ToArray(), forEachCalledOnItems.ToArray());
        }

        [TestMethod]
        public void WriteTest()
        {
            var items = new List<object>() { 3, 2, 1 };

            var comparer = new ObjectComparer();
            var context = TestObjectHelper.GetTestNFAppNanoTablesContext();
            var iut = new TestNanoReferenceTable(items, comparer, context);
            using (var ms = new MemoryStream())
            {
                using (var bw = new System.IO.BinaryWriter(ms, Encoding.Default, true))
                {
                    var writer = nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw);

                    // test
                    iut.Write(writer);

                    bw.Flush();

                }

                var bytesWritten = ms.ToArray();
                CollectionAssert.AreEqual(new byte[] { 0x33, 0, 0x32, 0, 0x31, 0 }, bytesWritten, String.Join(", ", bytesWritten.Select(i => i.ToString("X"))));
            }
        }

        private class TestNanoReferenceTable : nanoReferenceTableBase<object>
        {
            public List<HashSet<MetadataToken>> RemoveUnusedItemsCallParameters = new List<HashSet<MetadataToken>>();
            public List<object> AllocateSingleItemStringsCallParameters = new List<object>();
            public List<object> WriteSingleItemCallParameters = new List<object>();

            public TestNanoReferenceTable(IEnumerable<object> nanoTableItems, IEqualityComparer<object> comparer, nanoTablesContext context) : base(nanoTableItems, comparer, context)
            {
            }

            protected override void WriteSingleItem(nanoBinaryWriter writer, object item)
            {
                this.WriteSingleItemCallParameters.Add(item);
                writer.WriteString(item.ToString());
            }

            public override void RemoveUnusedItems(HashSet<MetadataToken> set)
            {
                this.RemoveUnusedItemsCallParameters.Add(set);
                base.RemoveUnusedItems(set);
            }

            protected override void AllocateSingleItemStrings(object item)
            {
                this.AllocateSingleItemStringsCallParameters.Add(item);
                base.AllocateSingleItemStrings(item);
            }
        }

        private class ObjectComparer : IEqualityComparer<object>
        {
            public List<KeyValuePair<object, object>> EqualsCallParameters = new List<KeyValuePair<object, object>>();
            public List<object> GetHashCodeCallParameters = new List<object>();

            public new bool Equals(object x, object y)
            {
                this.EqualsCallParameters.Add(new KeyValuePair<object, object>(x, y));
                return Object.Equals(x, y);
            }

            public int GetHashCode(object obj)
            {
                this.GetHashCodeCallParameters.Add(obj);
                return obj.GetHashCode();
            }
        }

    }
}
