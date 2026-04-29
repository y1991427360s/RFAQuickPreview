using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RFAQuickPreview.UI;

namespace RFAQuickPreview.App
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RfaQuickPreviewCommand : IExternalCommand
    {
        private static MainWindow _window;
        private static ExternalEvent _scanEvent;
        private static ScanExternalEventHandler _scanHandler;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (_window != null && _window.IsVisible)
            {
                _window.Activate();
                return Result.Succeeded;
            }

            _scanHandler = new ScanExternalEventHandler();
            _scanEvent = ExternalEvent.Create(_scanHandler);
            _scanHandler.SetExternalEvent(_scanEvent);

            _window = new MainWindow(_scanEvent, _scanHandler);
            _scanHandler.SetWindow(_window);
            _window.Closed += (sender, args) =>
            {
                _scanEvent.Dispose();
                _scanEvent = null;
                _scanHandler = null;
                _window = null;
            };
            _window.Show();

            return Result.Succeeded;
        }
    }
}
