using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;

internal sealed class ToolboxHttpHandler(TokenCredential credential, ExecutionProof proof) : DelegatingHandler(
    new HttpClientHandler { CheckCertificateRevocationList = true })
{
    private readonly TokenRequestContext _tokenContext = new(["https://ai.azure.com/.default"]);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? resourceUri = null;
        if (request.Content is not null)
        {
            string body = await request.Content.ReadAsStringAsync(cancellationToken);
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("method", out JsonElement method) &&
                method.GetString() == "resources/read")
            {
                resourceUri = root.GetProperty("params").GetProperty("uri").GetString();
            }
        }

        AccessToken token = await credential.GetTokenAsync(_tokenContext, cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        // Record only metadata, never authorization headers or tokens.
        if (resourceUri is not null)
        {
            proof.Record(response.IsSuccessStatusCode ? "resource-read" : "resource-read-failed",
                resourceUri, new { status = (int)response.StatusCode });
        }

        return response;
    }
}
