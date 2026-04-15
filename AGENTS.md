# AGENTS.md

## Project Purpose

This is a learning project for exploring a C#, Blazor, separate API, and Cytoscape-based code intelligence workspace. The app visualizes a mock codebase graph, lets the user inspect nodes, flows, endpoints, relationships, simple hotspots, and early AI-generated explanations.

## Solution Structure

- `AiCodeAssistant.Domain` - shared entities, enums, and DTO/contracts.
- `AiCodeAssistant.Application` - application interfaces and services, including mock graph data and explanation orchestration.
- `AiCodeAssistant.Infrastructure` - reserved for future infrastructure/provider implementations.
- `AiCodeAssistant.API` - ASP.NET Core API exposing graph and AI explanation endpoints.
- `AiCodeAssistant.Client` - Blazor UI, REST client/manager layer, workspace page, styling, and Cytoscape interop.

## Running Locally

Run the API and client separately:

```powershell
dotnet run --project AiCodeAssistant.API
dotnet run --project AiCodeAssistant.Client
```

The client currently calls the API at `http://localhost:5217`.

## Architecture Decisions

- Keep graph data contracts simple and explicit while the project is in a learning stage.
- Client workspace state is loaded from existing API endpoints and reused for node, flow, endpoint, and hotspot views.
- Cytoscape behavior lives in `AiCodeAssistant.Client/wwwroot/js/graph.js`.
- Blazor page structure and scoped styling live in `AiCodeAssistant.Client/Components/Pages/Home.razor` and `Home.razor.css`.
- AI explanation requests use structured DTOs rather than dumping raw project data.
- Hotspot scoring is deterministic client-side analysis, not AI-generated.

## Coding Expectations

- Prefer small, reviewable changes.
- Follow existing naming, layering, and UI patterns before adding new abstractions.
- Keep backend changes minimal unless a feature clearly needs them.
- Use structured DTOs for cross-layer data.
- Keep UI behavior readable and explicit; this is a learning project.
- Explain the plan before large edits or architectural changes.

## Do Not Change Casually

- Do not change backend graph contracts or endpoint routes without a clear reason.
- Do not replace the 3-panel workspace layout casually.
- Do not rewrite Cytoscape graph logic when a small interop function is enough.
- Do not add chatbot-style AI, memory, or whole-project prompting until explicitly requested.
- Do not introduce broad refactors, new frameworks, or unrelated cleanup in feature work.
- Do not run or restart the app unless explicitly asked by the user.

