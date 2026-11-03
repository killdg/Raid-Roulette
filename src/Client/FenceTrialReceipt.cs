using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RaidRoulette.TestingClient
{
    internal sealed class FenceTrialReceipt
    {
        private const string Path = "/client/raidroulette/fence-trial";
        private readonly string eventId;
        private readonly Task<string?> token;
        private bool sent;
        internal FenceTrialReceipt(string selectedEvent)
        {
            eventId = selectedEvent;
            token = Begin();
        }

        private async Task<string?> Begin()
        {
            try
            {
                string response = await Backend.Post(JsonConvert.SerializeObject(new { Action = "begin", EventId = eventId }), Path);
                return JObject.Parse(response)["token"]?.Value<string>();
            }
            catch (Exception error)
            {
                Plugin.Trace("[RR-FENCE] Could not obtain Trials receipt: " + error.GetBaseException().Message);
                return null;
            }
        }

        internal void Complete()
        {
            if (!sent)
            {
                sent = true;
                _ = Send();
            }
        }

        private async Task Send()
        {
            // No polling: one request at wave initialization, then at most three
            // completion sends. Keep the captured raid nonce across retries so a
            // late request cannot complete the next attempt of the same event.
            string? receipt = await token;
            if (string.IsNullOrEmpty(receipt))
            {
                return;
            }

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    string response = await Backend.Post(JsonConvert.SerializeObject(new { Action = "complete", EventId = eventId,
                         Token = receipt }), Path);
                    if (JObject.Parse(response)["accepted"]?.Value<bool>() == true)
                    {
                        Plugin.Trace("[RR-FENCE] Trials clear acknowledged: " + eventId);
                        return;
                    }
                }
                catch (Exception error)
                {
                    Plugin.Trace("[RR-FENCE] Trials receipt send failed: " + error.GetBaseException().Message);
                }

                if (attempt < 2)
                {
                    await Task.Delay(1000);
                }
            }

            Plugin.Trace("[RR-FENCE] Trials clear was not acknowledged; assignment will remain retryable.");
        }
    }
}
