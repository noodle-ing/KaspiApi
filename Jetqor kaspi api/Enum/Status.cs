using System.Runtime.Serialization;

namespace Jetqor_kaspi_api.Enum;

public enum Status
{
    cancelled, 
    completed,
    assembly,
    indelivery,
    waiting,
    packed,
    packaging,
    [EnumMember(Value = "return")]
    Return,
    return_request,
    
}