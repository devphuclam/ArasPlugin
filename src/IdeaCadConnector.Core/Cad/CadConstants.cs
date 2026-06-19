namespace IdeaCadConnector.Core.Cad
{
    // These constants describe local conventions only:
    // - IronCadPartExtension is needed when the client must place a local
    //   workspace placeholder file before the server has returned a real
    //   native filename.
    // - IronCadAuthoringTool / IronCadPartClassification are EXPECTED values
    //   returned by the server. The client compares against them, it does not
    //   author them when creating server records (see Method
    //   idea_EnsurePrimaryIronCadPartCad on the server).
    //
    // IronCadPartClassification holds the CAD class-structure path used by the
    // InnovatorSolutions database for an IronCAD part: CAD > Mechanical > Part,
    // stored as "Mechanical/Part" in the CAD.classification property.
    public static class CadConstants
    {
        public const string IronCadAuthoringTool = "IronCAD";
        public const string IronCadPartClassification = "Mechanical/Part";
        public const string IronCadPartExtension = ".ics";
    }
}
