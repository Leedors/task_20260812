using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace EmployeeDirectory.IntegrationTests;

internal static class TestHttp
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>&lt;input type="file"&gt; 업로드를 흉내낸다.</summary>
    public static Task<HttpResponseMessage> PostFileAsync(
        this HttpClient client,
        string content,
        string fileName,
        string contentType)
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var form = new MultipartFormDataContent { { file, "file", fileName } };

        return client.PostAsync("/api/employee", form);
    }

    /// <summary>&lt;textarea&gt; 직접 입력을 흉내낸다(본문에 원시 텍스트).</summary>
    public static Task<HttpResponseMessage> PostRawAsync(this HttpClient client, string content, string contentType)
        => client.PostAsync("/api/employee", new StringContent(content, Encoding.UTF8, contentType));

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(Options);

        value.Should().NotBeNull();
        return value!;
    }
}
