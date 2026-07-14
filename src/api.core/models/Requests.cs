
using Microsoft.AspNetCore.Mvc.Formatters;

namespace api.models;

public record QueryRequest
{
    public List<string> Keywords { get; init; }
    public MediaType Type { get; init; }
}
