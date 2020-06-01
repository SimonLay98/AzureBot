using System;
using System.Collections.Generic;
using System.Linq;
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
    public class ShutDownVmLandscapeDialog : CancelAndHelpDialog
    {
        public ShutDownVmLandscapeDialog() : base(nameof(ShutDownVmLandscapeDialog))
        {
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            //AddDialog(new AddVmToLandscapeMainDialog());
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                GetLandscapeStepAsync,
                ListVmsStepAsync,
                ConfirmStepAsync,
                FinalStepAsync,
            }));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> GetLandscapeStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (ChatbotDetails)stepContext.Options;
            details.RunWithCompleteLandscape = true;
            stepContext.Values["details"] = details;

            if (details.LandscapeTag == null)
            {
                var message = "Welche Landschaft soll herunter gefahren werden? Bitte wähle aus." + Environment.NewLine;

                IList<Choice> choices = new List<Choice>();
                var handler = new AzureHandler(details.User.Tenant, details.User.TokenResponse.Token);
                var landscapes = handler.GetAllAvailableLandscapes();
                if (!landscapes.Any())
                {
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text("Es existieren keine Landschaften in ihrem Azure Account. Diese müssen zuerst konfiguriert werden. Dies kann über das Azure Portal oder über diesen Bot geschehen."), cancellationToken);
                    return await stepContext.EndDialogAsync(null, cancellationToken);
                }
                foreach (var tag in landscapes)
                {
                    choices.Add(new Choice(tag));
                }

                message = message + "Folgende Landschaften können von dir herunter gefahren werden:";

                var promptMessage = MessageFactory.Text(message, message, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions() { Choices = choices, Prompt = promptMessage }, cancellationToken);
            }

            return await stepContext.NextAsync(new FoundChoice() { Value = details.LandscapeTag }, cancellationToken);
        }

        private async Task<DialogTurnResult> ListVmsStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (ChatbotDetails)stepContext.Values["details"];

            if (stepContext.Result is FoundChoice choice)
            {
                details.LandscapeTag = choice.Value;
                stepContext.Values["details"] = details;
            }
            var handler = new AzureHandler(details.User.Tenant, details.User.TokenResponse.Token);
            var vmsInThisLandscape = handler.GetListOfVmsFromSpecificLandscapeTag(details.LandscapeTag);
            var message = "Folgende VMs befinden sich in dieser Landschaft und werden herunter gefahren:" + Environment.NewLine;
            foreach (var vm in vmsInThisLandscape)
            {
                message = message + vm + Environment.NewLine;
            }

            return await stepContext.NextAsync(details, cancellationToken);
        }

        private async Task<DialogTurnResult> ConfirmStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (ChatbotDetails)stepContext.Result;

            var messageText = $"Bitte bestätige: Die Landschaft: {details.LandscapeTag} wird herunter gefahren." + Environment.NewLine;
            messageText = messageText + "Ist das korrekt?";
            stepContext.Values["details"] = details;
            var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
            return await stepContext.PromptAsync(nameof(ConfirmPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if ((bool)stepContext.Result)
            {
                var details = (ChatbotDetails)stepContext.Values["details"];
                details.RunWithCompleteLandscape = true;
                var handler = new AzureHandler(details.User.Tenant, details.User.TokenResponse.Token);
                handler.ShutDownVmsAsync(details);

                return await stepContext.EndDialogAsync(details, cancellationToken);
            }

            return await stepContext.EndDialogAsync(null, cancellationToken);
        }
    }
}
