using System;
using System.Reflection;
using IdeaCadConnector.IronCAD.NormalizeExport;
using Xunit;

namespace IdeaCadConnector.Tests
{
    public sealed class PdmLinkedExportCommandTests
    {
        [Fact]
        public void Command_AcceptsDocumentServiceViaConstructor()
        {
            var fakeService = new FakeSceneDocumentService();
            var fakeApp = DispatchProxy.Create<interop.ICApiIronCAD.IZBaseApp, StubIronCadApp>();
            var cmd = new IronCadNormalizeExportCommand(fakeApp, fakeService);
            Assert.NotNull(cmd);
        }

        [Fact]
        public void Command_DisposesServiceOnExecuteFailure()
        {
            var fakeService = new FakeSceneDocumentService();
            var fakeApp = DispatchProxy.Create<interop.ICApiIronCAD.IZBaseApp, StubIronCadApp>();
            var cmd = new IronCadNormalizeExportCommand(fakeApp, fakeService);
            try { cmd.Execute(); }
            catch { }
            Assert.True(fakeService.DisposeCalled);
        }

        [Fact]
        public void Service_OpenClose_Lifecycle()
        {
            var fakeService = new FakeSceneDocumentService();
            fakeService.SetDocument(@"C:\root.ics", true);

            var doc = fakeService.OpenDocument(@"C:\root.ics");
            Assert.True(fakeService.IsOpen);

            fakeService.CloseDocument();
            Assert.False(fakeService.IsOpen);
        }

        [Fact]
        public void Service_FailedOpen_ThrowsAndDoesNotSetIsOpen()
        {
            var fakeService = new FakeSceneDocumentService();
            Assert.Throws<InvalidOperationException>(() => fakeService.OpenDocument(@"C:\missing.ics"));
            Assert.False(fakeService.IsOpen);
        }

        [Fact]
        public void Service_DoubleClose_IsIdempotent()
        {
            var fakeService = new FakeSceneDocumentService();
            fakeService.CloseDocument();
            fakeService.CloseDocument();
            Assert.False(fakeService.IsOpen);
        }

        [Fact]
        public void Service_Dispose_ClosesOpenDocument()
        {
            var fakeService = new FakeSceneDocumentService();
            fakeService.SetDocument(@"C:\root.ics", true);
            fakeService.OpenDocument(@"C:\root.ics");
            fakeService.Dispose();
            Assert.False(fakeService.IsOpen);
        }

        public sealed class FakeSceneDocumentService : IIronCadSceneDocumentService
        {
            private bool _hasOpenDoc;
            private bool _disposed;

            public bool IsOpen => _hasOpenDoc && !_disposed;
            public bool DisposeCalled => _disposed;

            public void SetDocument(string path, bool canOpen) => _canOpen[path] = canOpen;

            private readonly System.Collections.Generic.Dictionary<string, bool> _canOpen =
                new System.Collections.Generic.Dictionary<string, bool>();

            public interop.ICApiIronCAD.IZSceneDoc OpenDocument(string filePath)
            {
                if (!_canOpen.TryGetValue(filePath, out var canOpen) || !canOpen)
                    throw new InvalidOperationException("NOT_FOUND: " + filePath);
                _hasOpenDoc = true;
                return null;
            }

            public void CloseDocument()
            {
                _hasOpenDoc = false;
            }

            public void Dispose()
            {
                _disposed = true;
                _hasOpenDoc = false;
            }
        }
    }
}
