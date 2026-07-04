
using Microsoft.AspNetCore.Mvc.Formatters;

public record QueryRequest
{
    public List<string> Keywords { get; init; }
    public MediaType Type { get; init; }
}
