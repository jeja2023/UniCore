using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Platform.WebApi.OpenApi;

public sealed class StandardErrorResponsesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        AddResponse(operation, "400", "请求参数错误");
        AddResponse(operation, "401", "未认证或令牌无效");
        AddResponse(operation, "403", "无权限访问");
        AddResponse(operation, "500", "系统内部错误");
    }

    private static void AddResponse(OpenApiOperation operation, string statusCode, string description)
    {
        if (operation.Responses.ContainsKey(statusCode))
        {
            return;
        }

        operation.Responses[statusCode] = new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new()
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchema>
                        {
                            ["success"] = new() { Type = "boolean" },
                            ["data"] = new() { Nullable = true },
                            ["errorCode"] = new() { Type = "string", Nullable = true },
                            ["message"] = new() { Type = "string", Nullable = true },
                            ["traceId"] = new() { Type = "string" }
                        }
                    }
                }
            }
        };
    }
}
