using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Chatbot.AzureHandling;
using Chatbot.Dialogs.Framework;
using Chatbot.Objects;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;

namespace Chatbot.Dialogs.ShutDownVMs
{
    public class ShutDownVmDialog : CancelAndHelpDialog
    {
        public ShutDownVmDialog() : base(nameof(ShutDownVmDialog))
        {
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                GetVmStepAsync,
                GetChoiceStepAsync,
                ConfirmStepAsync,
                FinalStepAsync,
            }));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> GetVmStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (ChatbotDetails)stepContext.Options;
            stepContext.Values["details"] = details;

            if (details.VmName == null)
            {
                var message = "Welche VM soll herunter gefahren werden? Bitte wähle aus." + Environment.NewLine;

                IList<Choice> choices = new List<Choice>();
                var handler = new AzureHandler(details.User.Tenant, details.User.TokenResponse.Token);
                foreach (var vm in handler.GetListOfVms())
                {
                    choices.Add(new Choice(vm.Name));
                }

                message = message + "Hier ist die Liste der Vms, welche von dir herunter gefahren werden können:";

                var promptMessage = MessageFactory.Text(message, message, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions() { Choices = choices, Prompt = promptMessage }, cancellationToken);
            }

            return await stepContext.NextAsync(new FoundChoice() { Value = details.VmName }, cancellationToken);
        }

        private async Task<DialogTurnResult> GetChoiceStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (ChatbotDetails)stepContext.Values["details"];

            if (stepContext.Result is FoundChoice choice)
            {
                details.VmName = choice.Value;
                stepContext.Values["details"] = details;
            }

            return await stepContext.NextAsync(details, cancellationToken);
        }

        private async Task<DialogTurnResult> ConfirmStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (ChatbotDetails)stepContext.Result;

            var messageText = $"Bitte bestätige: Die virtuelle Maschine {details.VmName} wird herunter gefahren. Ist das korrekt?";
            if (details.RunWithCompleteLandscape)
            {
                messageText = messageText + Environment.NewLine + "Die komplette Landschaft der Ressource wird ebenfalls herunter gefahren";
            }
            stepContext.Values["details"] = details;
            var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
            return await stepContext.PromptAsync(nameof(ConfirmPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if ((bool)stepContext.Result)
            {
                var details = (ChatbotDetails)stepContext.Values["details"];
                var handler = new AzureHandling.AzureHandler(details.User.Tenant, details.User.TokenResponse.Token);
                handler.ShutDownVmsAsync(details);

                return await stepContext.EndDialogAsync(details, cancellationToken);
            }

            return await stepContext.EndDialogAsync(null, cancellationToken);
        }
    }
}
