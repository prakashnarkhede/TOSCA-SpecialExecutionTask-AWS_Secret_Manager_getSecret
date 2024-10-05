using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tricentis.Automation.AutomationInstructions.Configuration;
using Tricentis.Automation.AutomationInstructions.Dynamic.Values;
using Tricentis.Automation.AutomationInstructions.TestActions;
using Tricentis.Automation.Creation;
using Tricentis.Automation.Engines;
using Tricentis.Automation.Engines.SpecialExecutionTasks;
using Tricentis.Automation.Engines.SpecialExecutionTasks.Attributes;
using Tricentis.Automation.Execution.Results;


/* 
 * Author: Prakash Narkhede
 * LinkedIn: https://www.linkedin.com/in/prakashnarkhede89/
 * Email: prakash@automationtalks.com
 * 
 * This Special Execution Task (SET) is designed for retrieving secrets from AWS Secrets Manager.
 * The code fetches AWS secrets based on the provided parameters (region, access key, secret name, etc.)
 * and can be used to securely access confidential data during Tosca test automation.
 */

namespace AWS_Secret_Manager_getSecret
{
    [SpecialExecutionTaskName("AWS_Secret_Manager_getSecret")]
    public class AwsSecretsManagerTask : SpecialExecutionTask
    {
        private readonly List<string> _logs = new List<string>(); // For detailed logging

        public AwsSecretsManagerTask(Validator validator) : base(validator) { }

        public override ActionResult Execute(ISpecialExecutionTaskTestAction testAction)
        {
            try
            {
                Log("Starting AWS Secrets Manager Retrieval Execution...");
                IParameter configParameters = testAction.GetParameter("AWS_Configurations", true);
                if (configParameters == null)
                {
                    Log("S3 Configuration parameters are missing.");
                    return new UnknownFailedActionResult("S3 Configuration parameters are missing.");
                }
                Log("Fetching S3 bucket configuration parameters...");

                // Fetch input parameters
                IInputValue regionInput = configParameters.GetChildParameter("Region", true, new[] { ActionMode.Input }).GetAsInputValue();
                IInputValue accessKeyInput = configParameters.GetChildParameter("AccessKey", true, new[] { ActionMode.Input }).GetAsInputValue();
                IInputValue secretKeyInput = configParameters.GetChildParameter("SecretAccess", true, new[] { ActionMode.Input }).GetAsInputValue();
                IInputValue secretNameInput = testAction.GetParameterAsInputValue("SecretName", true);

                if (secretNameInput == null) Log("Secret Name parameter is missing.");
                if (regionInput == null) Log("Region parameter is missing.");
                if (accessKeyInput == null) Log("AWS Access Key parameter is missing.");
                if (secretKeyInput == null) Log("AWS Secret Key parameter is missing.");

                if (secretNameInput == null || regionInput == null || secretKeyInput == null || accessKeyInput==null)
                    return new UnknownFailedActionResult("Missing required input parameters.");

                // Extract parameters
                string secretName = secretNameInput.Value;
                string region = regionInput.Value;
                string accessKey = accessKeyInput?.Value;
                string secretKey = secretKeyInput?.Value;

                Log($"Secret Name: {secretName}, Region: {region}");

                // Initialize AWS Secrets Manager client
                Log("Initializing AWS Secrets Manager client...");
                AmazonSecretsManagerClient secretsManagerClient;

                if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
                {
                    var awsCredentials = new BasicAWSCredentials(accessKey, secretKey);
                    secretsManagerClient = new AmazonSecretsManagerClient(awsCredentials, Amazon.RegionEndpoint.GetBySystemName(region));
                    Log("Initialized AWS Secrets Manager client with provided access and secret keys.");
                }
                else
                {
                    secretsManagerClient = new AmazonSecretsManagerClient(Amazon.RegionEndpoint.GetBySystemName(region));
                    Log("Initialized AWS Secrets Manager client with default credentials.");
                }

                // Retrieve secret value
                Log($"Retrieving secret value for: {secretName}");
                var secretValueResponse = GetSecretValueAsync(secretsManagerClient, secretName).Result;

                if (secretValueResponse == null || string.IsNullOrEmpty(secretValueResponse.SecretString))
                    return new UnknownFailedActionResult($"Failed to retrieve the secret value for: {secretName}.");

                string secretValue = secretValueResponse.SecretString;
                Log($"Successfully retrieved the secret value for: {secretName}");

                // Set the retrieved secret value as an output parameter
                IParameter outputParm = testAction.GetParameter("Output", true);
                if (outputParm.ActionMode == ActionMode.Buffer)
                {
                    IInputValue inputValue = outputParm.GetAsInputValue();
                    Buffers.Instance.SetBuffer(inputValue.Value, secretValue, false);
                    testAction.SetResultForParameter(outputParm, SpecialExecutionTaskResultState.Ok, string.Format("Buffer {0} set to actual secret value.", inputValue.Value));
                }
                else
                {
                    HandleActualValue(testAction, outputParm, Buffers.Instance.GetBuffer(outputParm.Name));
                }
                return new PassedActionResult($"Secret retrieved successfully.\n" + GetLogs());
            }
            catch (Exception ex)
            {
                Log($"An error occurred during secret retrieval: {ex.Message}");
                return new UnknownFailedActionResult($"Error during secret retrieval. Details:\n" + GetLogs());
            }
        }

        private async Task<GetSecretValueResponse> GetSecretValueAsync(IAmazonSecretsManager secretsManagerClient, string secretName)
        {
            try
            {
                var secretRequest = new GetSecretValueRequest
                {
                    SecretId = secretName
                };

                var response = await secretsManagerClient.GetSecretValueAsync(secretRequest);
                return response;
            }
            catch (AmazonSecretsManagerException e)
            {
                Log($"AWS Secrets Manager error: {e.Message}");
                throw;
            }
            catch (Exception e)
            {
                Log($"General error: {e.Message}");
                throw;
            }
        }

        private void Log(string message)
        {
            _logs.Add($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - {message}");
        }

        private string GetLogs()
        {
            return string.Join(Environment.NewLine, _logs);
        }
    }
}