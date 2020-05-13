using System;

[AttributeUsageAttribute(AttributeTargets.Class)]
public class CustomStructAttribute : Attribute {

    public CustomStructAttribute(params Type[] structs) { }

}
