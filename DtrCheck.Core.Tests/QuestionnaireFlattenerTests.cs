using DtrCheck.Core.Fhir;
using Hl7.Fhir.Model;

namespace DtrCheck.Core.Tests;

public class QuestionnaireFlattenerTests
{
    [Fact]
    public void SkipsGroupAndDisplayItems()
    {
        var questionnaire = new Questionnaire
        {
            Item =
            [
                new Questionnaire.ItemComponent
                {
                    LinkId = "group1",
                    Type = Questionnaire.QuestionnaireItemType.Group,
                    Text = "Patient Information",
                    Item =
                    [
                        new Questionnaire.ItemComponent { LinkId = "q1", Type = Questionnaire.QuestionnaireItemType.String, Text = "Last Name" },
                        new Questionnaire.ItemComponent { LinkId = "q2", Type = Questionnaire.QuestionnaireItemType.Display, Text = "Note: informational only" },
                    ],
                },
                new Questionnaire.ItemComponent { LinkId = "q3", Type = Questionnaire.QuestionnaireItemType.String, Text = "Diagnosis" },
                new Questionnaire.ItemComponent { LinkId = "q4", Type = Questionnaire.QuestionnaireItemType.String, Text = "Unmapped item" },
            ],
        };

        var items = QuestionnaireFlattener.Flatten(questionnaire);

        Assert.Equal(["q1", "q3", "q4"], items.Select(i => i.LinkId));
    }
}
