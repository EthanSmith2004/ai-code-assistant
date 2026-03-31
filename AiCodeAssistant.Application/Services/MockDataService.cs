using AiCodeAssistant.Domain.Entities;
using AiCodeAssistant.Application.Interfaces;

namespace AiCodeAssistant.Application.Services;

public class MockDataService : IGraphDataService
{
    public List<GraphNode> GetNodes()
    {
        return new List<GraphNode>
        {
            new GraphNode
            {
                Id = "page-login",
                Label = "LoginPage",
                Type = NodeType.Page,
                Layer = LayerType.Frontend,
                Description = "The frontend page where the user enters email and password."
            },
            new GraphNode
            {
                Id = "client-authapi",
                Label = "AuthApiClient",
                Type = NodeType.Component,
                Layer = LayerType.Frontend,
                Description = "Frontend client responsible for sending authentication requests to the API."
            },
            new GraphNode
            {
                Id = "endpoint-login",
                Label = "POST /api/auth/login",
                Type = NodeType.Endpoint,
                Layer = LayerType.Api,
                Description = "API endpoint for authenticating a user."
            },
            new GraphNode
            {
                Id = "controller-auth",
                Label = "AuthController",
                Type = NodeType.Controller,
                Layer = LayerType.Api,
                Description = "Controller that receives authentication requests."
            },
            new GraphNode
            {
                Id = "service-auth",
                Label = "AuthService",
                Type = NodeType.Service,
                Layer = LayerType.Application,
                Description = "Service that handles login validation and authentication logic."
            },
            new GraphNode
            {
                Id = "repo-user",
                Label = "UserRepository",
                Type = NodeType.Repository,
                Layer = LayerType.Data,
                Description = "Repository used to retrieve user records from the database."
            },
            new GraphNode
            {
                Id = "db-users",
                Label = "UsersTable",
                Type = NodeType.Database,
                Layer = LayerType.Data,
                Description = "Database table containing user account records."
            },
            new GraphNode
            {
                Id = "service-token",
                Label = "TokenService",
                Type = NodeType.Service,
                Layer = LayerType.Application,
                Description = "Service responsible for generating authentication tokens."
            }
        };
    }

    public List<GraphEdge> GetEdges()
    {
        return new List<GraphEdge>
        {
            new GraphEdge
            {
                Id = "edge-page-client",
                SourceId = "page-login",
                TargetId = "client-authapi",
                Relationship = RelationshipType.Calls
            },
            new GraphEdge
            {
                Id = "edge-client-endpoint",
                SourceId = "client-authapi",
                TargetId = "endpoint-login",
                Relationship = RelationshipType.Calls
            },
            new GraphEdge
            {
                Id = "edge-endpoint-controller",
                SourceId = "endpoint-login",
                TargetId = "controller-auth",
                Relationship = RelationshipType.MapsTo
            },
            new GraphEdge
            {
                Id = "edge-controller-service",
                SourceId = "controller-auth",
                TargetId = "service-auth",
                Relationship = RelationshipType.Uses
            },
            new GraphEdge
            {
                Id = "edge-service-repo",
                SourceId = "service-auth",
                TargetId = "repo-user",
                Relationship = RelationshipType.ReadsFrom
            },
            new GraphEdge
            {
                Id = "edge-repo-db",
                SourceId = "repo-user",
                TargetId = "db-users",
                Relationship = RelationshipType.Queries
            },
            new GraphEdge
            {
                Id = "edge-service-token",
                SourceId = "service-auth",
                TargetId = "service-token",
                Relationship = RelationshipType.Uses
            }
        };
    }

    public List<CodeFlow> GetFlows()
    {
        return new List<CodeFlow>
        {
            new CodeFlow
            {
                Id = "flow-login",
                Name = "Login Flow",
                Description = "The full flow that happens when a user logs into the system.",
                NodeIds = new List<string>
                {
                    "page-login",
                    "client-authapi",
                    "endpoint-login",
                    "controller-auth",
                    "service-auth",
                    "repo-user",
                    "db-users",
                    "service-token"
                }
            }
        };
    }

    public List<EndpointInfo> GetEndpoints()
    {
        return new List<EndpointInfo>
        {
            new EndpointInfo
            {
                NodeId = "endpoint-login",
                HttpMethod = "POST",
                Route = "/api/auth/login",
                RequestType = "LoginRequest",
                ResponseType = "LoginResponse"
            }
        };
    }
}