using AiCodeAssistant.Domain.Graph;

namespace AiCodeAssistant.Application.Interfaces;

public interface ICodeGraphSimplifier
{
    CodeGraph Simplify(CodeGraph graph);
}
