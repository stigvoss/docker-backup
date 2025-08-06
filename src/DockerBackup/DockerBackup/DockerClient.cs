using System.ComponentModel;
using System.Net.Http.Json;
using System.Net.Sockets;

namespace DockerBackup;

public class DockerClient : IDisposable
{
    private readonly HttpClient httpClient;
    private const string SocketPath = "/var/run/docker.sock";
    
    public DockerClient()
    {
        SocketsHttpHandler handler = new()
        {
            ConnectCallback = async (_, token) =>
            {
                UnixDomainSocketEndPoint udsEndPoint = new(SocketPath);
                Socket socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                await socket.ConnectAsync(udsEndPoint, token);
                return new NetworkStream(socket, true);
            }
        };
        
        this.httpClient = new(handler)
        {
            BaseAddress = new Uri("http://localhost/")
        };
    }
    
    /// <summary>
    /// Asynchronously retrieves a list of Docker containers.
    /// </summary>
    /// <returns> An asynchronous enumerable collection of <see cref="DockerContainer"/> objects.</returns>
    public async IAsyncEnumerable<DockerContainer> GetContainersAsync()
    {
        HttpResponseMessage response = await this.httpClient.GetAsync("/containers/json");
        response.EnsureSuccessStatusCode();
        
        await foreach (DockerContainer? container in response.Content.ReadFromJsonAsAsyncEnumerable<DockerContainer>())
        {
            if (container is not null)
            {
                yield return container;
            }
        }
    }
    
    /// <summary>
    /// Asynchronously retrieves a specific Docker container by its ID.
    /// </summary>
    /// <param name="containerId">The ID of the Docker container to retrieve.</param>
    /// <returns>A single <see cref="DockerContainer"/> object if found, otherwise null.</returns>
    public async Task<DockerContainer?> GetContainerAsync(string containerId)
    {
        HttpResponseMessage response = await this.httpClient.GetAsync($"/containers/{containerId}/json");
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<DockerContainer>();

    }

    /// <summary>
    /// Asynchronously executes a command in a specified Docker container.
    /// </summary>
    /// <param name="container">The Docker container in which to execute the command.</param>
    /// <param name="command">The command to execute in the container.</param>
    /// <returns>The output of the command execution as a string, or null if the command fails.</returns>
    /// <exception cref="InvalidOperationException">Failed to start the command execution.</exception>
    public async Task<string?> ExecuteCommandAsync(DockerContainer container, string command)
    {
        CreateExecInstanceResponse? createInstanceResponse = await CreateExecInstance(container, command);

        if (string.IsNullOrWhiteSpace(createInstanceResponse?.Id))
        {
            throw new InvalidOperationException("Failed to create exec instance.");
        }
        
        return await StartExecInstance(createInstanceResponse.Id);
    }

    /// <summary>
    /// Starts an exec instance in a Docker container.
    /// </summary>
    /// <param name="execInstanceId">The ID of the exec instance to start.</param>
    /// <returns>The output of the exec instance as a string, or null if the start fails.</returns>
    /// <exception cref="InvalidOperationException">Failed to start the exec instance.</exception>
    private async Task<string?> StartExecInstance(string execInstanceId)
    {
        HttpResponseMessage response = await this.httpClient.PostAsJsonAsync($"/exec/{execInstanceId}/start", new
        {
            Detach = false,
            Tty = false
        });
        
        if (!response.IsSuccessStatusCode)
        {
            ErrorResponse? errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            throw new InvalidOperationException($"Failed to start exec instance: {errorResponse}");
        }

        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Creates an exec instance in a Docker container to run a command.
    /// </summary>
    /// <param name="container">The Docker container in which to create the exec instance.</param>
    /// <param name="command">The command to execute in the container.</param>
    /// <returns>A <see cref="CreateExecInstanceResponse"/> containing the ID of the created exec instance, or null if creation fails.</returns>
    private async Task<CreateExecInstanceResponse?> CreateExecInstance(DockerContainer container, string command)
    {
        HttpResponseMessage response = await this.httpClient.PostAsJsonAsync($"/containers/{container.Id}/exec", new
        {
            AttachStdin = false,
            AttachStdout = true,
            AttachStderr = true,
            Tty = false,
            Cmd = new[] { "/bin/sh", "-c", command }
        });
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CreateExecInstanceResponse>();
    }

    public void Dispose()
    {
        this.httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
        
    private record CreateExecInstanceResponse(string Id);

    private record ErrorResponse(string Message)
    {
        public override string ToString() => Message;
    }
}
