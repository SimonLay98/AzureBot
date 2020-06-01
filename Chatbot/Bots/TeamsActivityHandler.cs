using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace Chatbot.Bots
{
    public class TeamsActivityHandler : ActivityHandler
    {
        public override Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (turnContext == null)
            {
                throw new ArgumentNullException(nameof(turnContext));
            }

            if (turnContext.Activity == null)
            {
                throw new ArgumentException($"{nameof(turnContext)} must have non-null Activity.");
            }

            if (turnContext.Activity.Type == null)
            {
                throw new ArgumentException($"{nameof(turnContext)}.Activity must have non-null Type.");
            }

            //turnContext.OnSendActivities(async (ctx, activities, nextSend) =>
            //{
            //    foreach (var activity in activities)
            //    {
            //        if (activity.ChannelId != "msteams") continue;
            //        if (activity.Attachments == null) continue;
            //        if (!activity.Attachments.Any()) continue;
            //        if (activity.Attachments[0].ContentType != "application/vnd.microsoft.card.signin") continue;
            //        if (!(activity.Attachments[0].Content is SigninCard card)) continue;
            //        if (!(card.Buttons is CardAction[] buttons)) continue;
            //        if (!buttons.Any()) continue;

            //        // Modify button type to openUrl as signIn is not working in teams
            //        buttons[0].Type = ActionTypes.OpenUrl;
            //    }

            //    // run full pipeline
            //    return await nextSend().ConfigureAwait(false);
            //});

            switch (turnContext.Activity.Type)
            {
                case ActivityTypes.Invoke:
                    return OnInvokeActivityAsync(new DelegatingTurnContext<IInvokeActivity>(turnContext), cancellationToken);

                default:
                    return base.OnTurnAsync(turnContext, cancellationToken);
            }
            
        }

        protected virtual Task OnInvokeActivityAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            switch (turnContext.Activity.Name)
            {
                case "signin/verifyState":
                    return OnSigninVerifyStateAsync(turnContext, cancellationToken);

                default:
                    return Task.CompletedTask;
            }
        }

        protected virtual Task OnSigninVerifyStateAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
