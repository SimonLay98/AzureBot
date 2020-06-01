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

namespace Chatbot.Dialogs.Utils
{
    public class SetObligationVmsForLandscapeDialog : CancelAndHelpDialog
    {
        public SetObligationVmsForLandscapeDialog() : base(nameof(SetObligationVmsForLandscapeDialog))
        {
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
               AskForLandscapeStepAsync,
               AskForObligationVmsStepAsync,
               SetObligationVmsStepAsync

            }));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> AskForLandscapeStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (ChatbotDetails)stepContext.Options;
            stepContext.Values["details"] = details;
            var handler = new AzureHandler(details.User.Tenant, details.User.TokenResponse.Token);
            var landscapes = handler.GetAllAvailableLandscapes();

            IList<Choice> choices = new List<Choice>();

            foreach (var tag in landscapes)
            {
                choices.Add(new Choice(tag));
            }

            var message = "Für welche dieser Landschaften sollen die verpflichtenden VMs konfiguriert werden?";

            var promptMessage = MessageFactory.Text(message, message, InputHints.ExpectingInput);
            return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions() { Choices = choices, Prompt = promptMessage }, cancellationToken);
        }


        private async Task<DialogTurnResult> AskForObligationVmsStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (ChatbotDetails)stepContext.Values["details"];

            if (stepContext.Result is FoundChoice choice)
            {
                details.LandscapeTag = choice.Value;

                var message = "Die Landschaft " + details.LandscapeTag + " hat folgende VMs." + Environment.NewLine +
                              " Bitte tippe die IDs der VMs mit Leerzeichen getrennt ein, welche als notwendig gekennzeichnet werden sollen.";

                var handler = new AzureHandler(details.User.Tenant, details.User.TokenResponse.Token);
                int id = 0;
                var dic = new Dictionary<int, string>();
                foreach (var vm in handler.GetListOfVmsFromSpecificLandscapeTag(details.LandscapeTag))
                {
                    dic.Add(id, vm);
                    message = message + Environment.NewLine + id + ".) " + vm;
                    id++;
                }

                stepContext.Values["dic"] = dic;
                var promptMessage = MessageFactory.Text(message, message, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
            }
            else
            {
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> SetObligationVmsStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (ChatbotDetails)stepContext.Values["details"];
            var handler = new AzureHandler(details.User.Tenant, details.User.TokenResponse.Token);
            var result = stepContext.Result.ToString().Split(" ");
            var dic = (Dictionary<int, string>)stepContext.Values["dic"];
            var message = "Jeder VM, welche als notwendig markiert wurde wird jetzt ein Tag in Azure hinzugefügt. Dies wird etwas Zeit benötigen, bis die Daten in Azure gespeichert wurden.";
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(message, message, InputHints.IgnoringInput), cancellationToken);

            foreach (var number in result)
            {
                try
                {
                    if (Int16.TryParse(number, out var numberAsInt))
                    {
                        handler.AddObligationForLandscapeTagToVmAsync(dic[numberAsInt], details.LandscapeTag);
                    }
                    else
                    {
                        throw new Exception("Tryparse hat nicht funktioniert.");
                    }
                }
                catch
                {
                    var message2 = "Die Antwort darf nur aus gültigen IDs mit Leerzeichen getrennt bestehen!";
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text(message2, message2, InputHints.IgnoringInput), cancellationToken);
                    return await stepContext.ReplaceDialogAsync(nameof(SetObligationVmsForLandscapeDialog), details, cancellationToken);
                }
            }

            var text = "Die angegebenen VMs sind für das nächste Starten der Landschaft als notwendig hinterlegt.";
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(text, text, InputHints.IgnoringInput), cancellationToken);
            return await stepContext.EndDialogAsync(details, cancellationToken);
        }
    }
}
