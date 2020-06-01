using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Chatbot.Dialogs.Framework;
using Chatbot.Dialogs.Utils;
using Chatbot.Objects;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;

namespace Chatbot.Dialogs
{
    public class ConfigureDialog : CancelAndHelpDialog
    {
        public ConfigureDialog() : base(nameof(ConfigureDialog))
        {
            AddDialog(new SetObligationVmsForLandscapeDialog());
            AddDialog(new AddVmToLandscapeMainDialog());

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                WhatToConfigureStepAsync,
                ActStepAsync,
                FinalStepAsync
            }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> WhatToConfigureStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (ChatbotDetails)stepContext.Options;
            stepContext.Values["details"] = details;

            IList<Choice> choices = new List<Choice>();
            choices.Add(new Choice("Eine VM einer Landschaft hinzufügen"));
            choices.Add(new Choice("Einer Landschaft alle VMs, welche notwendig sind, mitteilen."));

            var message = "Bitte wähle die Konfiguration, welche du durchführen möchtest. Das entfernen von VMs aus einer Landschaft bzw. das entfernen von Pflicht-Tags ist bisher noch nur über das Azure Portal möglich.";
            var promptMessage = MessageFactory.Text(message, message, InputHints.ExpectingInput);
            return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions() { Choices = choices, Prompt = promptMessage,Style = ListStyle.SuggestedAction}, cancellationToken);
        }

        private async Task<DialogTurnResult> ActStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = stepContext.Values["details"];

            if (stepContext.Result is FoundChoice choice)
            {
                if (choice.Value == "Eine VM einer Landschaft hinzufügen")
                {
                    return await stepContext.BeginDialogAsync(nameof(AddVmToLandscapeMainDialog), details, cancellationToken);
                }
                else if (choice.Value == "Einer Landschaft alle VMs, welche notwendig sind, mitteilen.")
                {
                    return await stepContext.BeginDialogAsync(nameof(SetObligationVmsForLandscapeDialog), details, cancellationToken);
                }
                else
                {
                    return await stepContext.EndDialogAsync(null, cancellationToken);
                }
            }

            return await stepContext.EndDialogAsync(null, cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (ChatbotDetails)stepContext.Result;

            return await stepContext.EndDialogAsync(details, cancellationToken);
        }
    }
}
