using System;
using System.Reflection;

namespace IdeaCadConnector.Tests
{
    public class StubIronCadApp : DispatchProxy
    {
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            var name = targetMethod.Name;
            if (name == "get_ActiveDoc" || name == "get_IsOpen" || name == "ActiveDoc")
                return null;
            if (name == "get_Type" || name == "get_ProductType" || name == "get_ApiVersion")
                return Activator.CreateInstance(targetMethod.ReturnType);
            if (name == "OpenFile")
                return null;
            return targetMethod.ReturnType.IsValueType && targetMethod.ReturnType != typeof(void)
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }
}
