using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Hollan.Functions.CircuitBreaker
{
    public class CircuitHttpTrigger {
        
        [FunctionName("FlagFailure")]
        public async Task<IActionResult> FlagFailure(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "circuit/{circuitId}/failure")] HttpRequest req,
            string circuitId,
            [OrchestrationClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            log.LogInformation($"HTTP Trigger for failure fired for circuit {circuitId}");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonConvert.DeserializeObject<FailureRequest>(requestBody);
            
            // Set the time the request was processed
            data.FailureTime = DateTime.UtcNow;
            
            await client.SignalEntityAsync(new EntityId(nameof(CircuitEntity), circuitId), nameof(CircuitEntity.AddFailure), data);

            return new AcceptedResult();
        }
        
    }

    public class FailureRequest 
    {
        public Guid RequestId { get; set; }
        public DateTime FailureTime { get; set; }
        public string InstanceId { get; set; }
    }
}