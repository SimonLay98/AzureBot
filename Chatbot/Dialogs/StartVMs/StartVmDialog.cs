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

namespace Chatbot.Dialogs.StartVMs
{
    public class StartVmDialog : CancelAndHelpDialog
    {
        public StartVmDialog() : base(nameof(StartVmDialog))
        {
            AddDialog(new TextPrompt(nameof(TextPrompt)));
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
                var message = "Welche VM soll gestartet werden? Bitte wähle aus." + Environment.NewLine;

                IList<Choice> choices = new List<Choice>();
                var handler = new AzureHandler(details.User.Tenant, details.User.TokenResponse.Token);
                foreach (var vm in handler.GetListOfVms())
                {
                    choices.Add(new Choice(vm.Name));
                }
                //TODO hier bereits laufende VMS nicht anzeigen

                message = message + "Hier ist die Liste der Vms, welche von dir gestartet werden können:";
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

            var messageText = $"Bitte bestätige: Die virtuelle Maschine {details.VmName} wird gestartet. Ist das korrekt?";
            if (details.RunWithCompleteLandscape)
            {
                if (details.OnlyObligationVms)
                {
                    messageText = messageText + Environment.NewLine + "Die als notwendig angegebenen VMs der Landschaft werden ebenfalls gestartet";
                }
                else
                {
                    messageText = messageText + Environment.NewLine + "Die komplette Landschaft der Ressource wird ebenfalls gestartet";
                }
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
                var handler = new AzureHandler(details.User.Tenant, details.User.TokenResponse.Token);
                handler.StartVmsAsync(details);

                return await stepContext.EndDialogAsync(details, cancellationToken);
            }

            return await stepContext.EndDialogAsync(null, cancellationToken);
        }
    }
}
