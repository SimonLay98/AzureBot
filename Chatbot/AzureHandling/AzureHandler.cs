using System.Collections.Generic;
using System.Linq;
using Chatbot.Objects;
using Microsoft.Azure.Management.Compute.Fluent;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Rest;

namespace Chatbot.AzureHandling
{
    public class AzureHandler
    {
        private readonly IAzure _azure;

        public AzureHandler(string tenant, string token)
        {
            //Bot impersoniert mit AzureAD Token
            var tokenCredentials = new TokenCredentials(token);
            var azureCredentials = new AzureCredentials(tokenCredentials, tokenCredentials, tenant, AzureEnvironment.AzureGlobalCloud);

            _azure = Azure.Configure()
               .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
               .Authenticate(azureCredentials)
               .WithDefaultSubscription();


            //TODO Fehlerbehandlung beim Azure Handling
            //vor allem auch was passiert wenn VMs nicht gestartet oder beendet werden konnten
        }

        public List<IVirtualMachine> GetListOfVms()
        {
            //TODO ausprobieren was passiert wenn VMs in unterschiedlichen Abonnements/Verzeichnissen liegen
            var allVMs = _azure.VirtualMachines;
            var listOfVMs = allVMs.List().ToList();
            return listOfVMs;
        }

        public void StartVmsAsync(ChatbotDetails details)
        {
            var vmList = _azure.VirtualMachines.List().ToList();
            if (details.RunWithCompleteLandscape)
            {
                var otherVmsToStart = vmList.Where(x => x.Tags.Any(y => y.Key.Contains("Landschaft")) && x.Tags.Values.Contains(details.LandscapeTag)).ToList();
                if (details.OnlyObligationVms)
                {
                    foreach (var virtualMachine in GetObligationVmsForLandscapeTag(details.LandscapeTag))
                    {
                        virtualMachine.StartAsync();
                    }
                }
                else
                {
                    foreach (var vm in otherVmsToStart)
                    {
                        vm.StartAsync();
                    }
                }
            }
            else
            {
                vmList.First(x => x.Name == details.VmName).StartAsync();
            }
        }

        public async void ShutDownVmsAsync(ChatbotDetails details)
        {
            var vmList = _azure.VirtualMachines.List().ToList();
            if (details.RunWithCompleteLandscape)
            {
                var vmsToShutDown = vmList.Where(x => x.Tags.Any(y => y.Key.Contains("Landschaft")) && x.Tags.Values.Contains(details.LandscapeTag.Replace("Landschaft ", ""))).ToList();
                foreach (var vm in vmsToShutDown)
                {
                    await vm.DeallocateAsync();
                }
            }
            else
            {
                await vmList.First(x => x.Name == details.VmName).DeallocateAsync();
            }

        }

        public void AddLandscapeTagToVmAsync(string tag, string vmName)
        {
            var vm = _azure.VirtualMachines.List().First(x => x.Name == vmName);

            int i = 0;
            foreach (var landscapeTag in vm.Tags.Where(x => x.Key.Contains("Landschaft")))
            {
                i++;
            }

            vm.Update().WithTag("Landschaft:" + i, tag).ApplyAsync();
        }

        public List<string> GetAllAvailableLandscapes()
        {
            var list = new List<string>();

            foreach (var vm in _azure.VirtualMachines.List())
            {
                if (vm.Tags.Any(x => x.Key.Contains("Landschaft") && x.Value != "Pflicht"))
                {
                    var landscapeTags = vm.Tags.Where(x => x.Key.Contains("Landschaft") && x.Value != "Pflicht");
                    foreach (var landscapeTag in landscapeTags)
                    {
                        if (!list.Contains(landscapeTag.Value))
                        {
                            list.Add(landscapeTag.Value);
                        }
                    }
                }
            }
            return list;
        }

        public List<string> GetListOfVmsFromSpecificLandscapeTag(string landscapeTag)
        {
            var list = new List<string>();
            foreach (var vm in GetListOfVms())
            {
                if (vm.Tags.Values.Contains(landscapeTag))
                {
                    list.Add(vm.Name);
                }
            }

            return list;
        }

        public void AddObligationForLandscapeTagToVmAsync(string vmName, string landscapeTag)
        {
            var vm = _azure.VirtualMachines.List().First(x => x.Name == vmName);

            vm.Update().WithTag(landscapeTag, "Pflicht").ApplyAsync();
        }

        public IEnumerable<IVirtualMachine> GetObligationVmsForLandscapeTag(string landscapeTag)
        {
            return _azure.VirtualMachines.List().Where(x => x.Tags.Any(y => y.Value == landscapeTag && x.Key.Contains(landscapeTag) && x.Key == "Pflicht"));
        }

        public List<string> GetObligationVmsForLandscapeTagAsList(string landscapeTag)
        {
            return GetObligationVmsForLandscapeTag(landscapeTag).Select(x => x.Name).ToList();
        }
    }
}
