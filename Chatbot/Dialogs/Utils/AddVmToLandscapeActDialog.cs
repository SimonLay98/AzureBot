using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chatbot.AzureHandling;
using Chatbot.Dialogs.Framework;
using Chatbot.Objects;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;

namespace Chatbot.Dialogs.Utils
{
    public class AddVmToLandscapeActDialog : CancelAndHelpDialog
    {
        public AddVmToLandscapeActDialog() : base(nameof(AddVmToLandscapeActDialog))
        {
            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                ListTagsStepAsync,
                NameStepAsync,
                ConfirmStepAsync,
                FinalStepAsync
            }));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> ListTagsStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (ChatbotDetails)stepContext.Options;

            stepContext.Values["details"] = details;
            var handler = new AzureHandler(details.User.Tenant, details.User.TokenResponse.Token);
            var tags = handler.GetListOfVms().FirstOrDefault(x => x.Name == details.VmName)?.Tags;
            if (tags != null)
            {
                var message = "Folgende Tags besitzt diese VM bereits:";
                foreach (var vmTag in tags)
                {
                    message = message + Environment.NewLine + vmTag;
                }

                await stepContext.Context.SendActivityAsync(MessageFactory.Text(message, message, InputHints.IgnoringInput), cancellationToken);
            }
            return await stepContext.NextAsync(true, cancellationToken);
        }

        private async Task<DialogTurnResult> NameStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (ChatbotDetails)stepContext.Values["details"];

            if ((bool)stepContext.Result)
            {
                var message = "Wie soll die Landschaft heißen?" + Environment.NewLine;
                var vmName = details.VmName;
                message = message + "Folgende Landschaften existieren in deinem Azure:" + Environment.NewLine;

                var handler = new AzureHandler(details.User.Tenant, details.User.TokenResponse.Token);
                foreach (var tag in handler.GetAllAvailableLandscapes())
                {
                    message = message + tag + Environment.NewLine;
                }
                message = message + "Bitte tippe als Antwort den Namen der Landschaft zu der die VM " + vmName + " hinzugehört" + Environment.NewLine;
                var promptMessage = MessageFactory.Text(message, message, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
            }
            else
            {
                details.RunWithCompleteLandscape = false;
                return await stepContext.EndDialogAsync(details, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> ConfirmStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var landscapeTagName = stepContext.Result.ToString();
            var details = (ChatbotDetails)stepContext.Values["details"];
            details.LandscapeTag = landscapeTagName;

            stepContext.Values["details"] = details;

            var message = "Bitte bestätige:" + Environment.NewLine + "Der VM " + details.VmName + " wird der Tag:" + Environment.NewLine + "Landschaft " + landscapeTagName + Environment.NewLine + "hinzugefügt." + Environment.NewLine + "Ist das korrekt?";
            var promptMessage = MessageFactory.Text(message, message, InputHints.ExpectingInput);
            return await stepContext.PromptAsync(nameof(ConfirmPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
        }


        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var details = (ChatbotDetails)stepContext.Values["details"];

            if ((bool)stepContext.Result)
            {
                var handler = new AzureHandler(details.User.Tenant, details.User.TokenResponse.Token);
                var message = "Tag wird hinzugefügt. Dies wird im Hintergrund eine kurze Zeit benötigen. Bitte beende den Bot nicht.";
                await stepContext.Context.SendActivityAsync(MessageFactory.Text(message), cancellationToken);
                handler.AddLandscapeTagToVmAsync(details.LandscapeTag, details.VmName);

                return await stepContext.EndDialogAsync(details, cancellationToken);
            }
            return await stepContext.EndDialogAsync(details, cancellationToken);
        }

    }
}
