using DtrCheck.Core.Models;
using Hl7.Fhir.Model;

namespace DtrCheck.Core.Fhir;

public static class QuestionnaireFlattener
{
    private static readonly HashSet<Questionnaire.QuestionnaireItemType?> NonDataItemTypes =
    [
        Questionnaire.QuestionnaireItemType.Group,
        Questionnaire.QuestionnaireItemType.Display,
    ];

    /// <summary>Walks a Questionnaire.item tree and returns only leaf (data-bearing) items.</summary>
    public static List<QuestionnaireItem> Flatten(Questionnaire questionnaire)
    {
        var items = new List<QuestionnaireItem>();
        Walk(questionnaire.Item, items);
        return items;
    }

    private static void Walk(List<Questionnaire.ItemComponent>? nodes, List<QuestionnaireItem> items)
    {
        if (nodes is null) return;
        foreach (var node in nodes)
        {
            if (!NonDataItemTypes.Contains(node.Type))
            {
                items.Add(new QuestionnaireItem(node.LinkId ?? string.Empty, node.Text ?? string.Empty, node.Type?.ToString() ?? string.Empty));
            }
            Walk(node.Item, items);
        }
    }
}
