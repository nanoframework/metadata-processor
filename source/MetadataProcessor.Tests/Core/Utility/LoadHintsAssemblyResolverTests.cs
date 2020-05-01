using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Utility
{
    [TestClass]
    public class LoadHintsAssemblyResolverTests
    {
        [TestMethod]
        public void ConstructorTest()
        {
            // test
            using (var iut = new LoadHintsAssemblyResolver(new Dictionary<string, string>()))
            {
                // no op
            };
        }

        [TestMethod]
        public void ResolveSomethingKnownTest()
        {
            DoResolveSomethingKnownTest((iut, assemblyNameReference) =>
            {
                // test
                Mono.Cecil.AssemblyDefinition ret = iut.Resolve(assemblyNameReference);
                return ret;
            });

        }

        [TestMethod]
        public void ResolveSomethingKnownWithReaderParamTest()
        {
            DoResolveSomethingKnownTest((iut, assemblyNameReference) =>
            {
                Mono.Cecil.AssemblyDefinition ret = null;

                var readerParameters = new Mono.Cecil.ReaderParameters();

                // test
                ret = iut.Resolve(assemblyNameReference, readerParameters);

                return ret;
            });
        }

        private void DoResolveSomethingKnownTest(Func<LoadHintsAssemblyResolver, Mono.Cecil.AssemblyNameReference, Mono.Cecil.AssemblyDefinition> resolveFuncToTest)
        {
            var thisAssemblyName = this.GetType().Assembly.GetName();
            var assemblyNameReference = new Mono.Cecil.AssemblyNameReference(thisAssemblyName.Name, thisAssemblyName.Version);

            using (var iut = new LoadHintsAssemblyResolver(new Dictionary<string, string>()))
            {
                var r = resolveFuncToTest(iut, assemblyNameReference);

                Assert.IsNotNull(r);
                Assert.AreEqual(thisAssemblyName.FullName, r.FullName);
            }
        }

        [TestMethod]
        public void ResolveSomethingWeirdTest()
        {
            DoResolveSomethingWeirdTest((iut, assemblyNameReference) =>
            {
                // test
                Mono.Cecil.AssemblyDefinition ret = iut.Resolve(assemblyNameReference);
                return ret;
            });
        }

        [TestMethod]
        public void ResolveSomethingWeirdWithReaderParamTest()
        {
            DoResolveSomethingWeirdTest((iut, assemblyNameReference) =>
            {
                Mono.Cecil.AssemblyDefinition ret = null;

                var readerParameters = new Mono.Cecil.ReaderParameters();

                // test
                ret = iut.Resolve(assemblyNameReference, readerParameters);

                return ret;
            });
        }

        private void DoResolveSomethingWeirdTest(Func<LoadHintsAssemblyResolver, Mono.Cecil.AssemblyNameReference, Mono.Cecil.AssemblyDefinition> resolveFuncToTest)
        {
            var weirdAssemblyName = Guid.NewGuid().ToString("N");
            var thisAssembly = this.GetType().Assembly;
            var loadHints = new Dictionary<string, string>();

            var assemblyNameReference = new Mono.Cecil.AssemblyNameReference(weirdAssemblyName, new Version(999, 999));

            using (var iut = new LoadHintsAssemblyResolver(loadHints))
            {
                try
                {
                    var r = resolveFuncToTest(iut, assemblyNameReference);
                    Assert.Fail("no exception thrown");
                }
                catch
                {
                    // no op
                }
            }


            loadHints.Add(weirdAssemblyName, thisAssembly.Location);

            using (var iut = new LoadHintsAssemblyResolver(loadHints))
            {
                // test
                var r = resolveFuncToTest(iut, assemblyNameReference);

                Assert.IsNotNull(r);
                Assert.AreEqual(r.FullName, thisAssembly.FullName);
            }
        }


    }
}
