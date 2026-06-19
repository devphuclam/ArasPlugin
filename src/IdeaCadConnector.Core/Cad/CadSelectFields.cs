namespace IdeaCadConnector.Core.Cad
{
    public static class CadSelectFields
    {
        public const string CadFull =
            "id,item_number,classification,authoring_tool,major_rev,state,generation,native_file,locked_by_id";

        public const string PartSearch =
            "id,item_number,name,description,major_rev,state,classification";
    }
}
