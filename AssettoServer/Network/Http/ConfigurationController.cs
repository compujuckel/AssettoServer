using System;
using AssettoServer.Network.Http.Authentication;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Network.Http.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace AssettoServer.Network.Http;

[ApiController]
[Route("/api/configuration")]
[Authorize(AuthenticationSchemes = ACClientAuthenticationSchemeOptions.Scheme, Roles = "Administrator")]
public class ConfigurationController : ControllerBase
{
    private readonly ConfigurationSerializer _serializer;
    private readonly ACServerConfiguration _configuration;

    public ConfigurationController(ConfigurationSerializer serializer, ACServerConfiguration configuration)
    {
        _serializer = serializer;
        _configuration = configuration;
    }

    [HttpGet("")]
    [Produces("text/x-lua")]
    public ConfigurationObject? GetConfiguration()
    {
        return _serializer.ParseSection(_configuration);
    }

    [HttpPost("")]
    [Produces("text/x-lua")]
    public SetConfigurationResponse SetConfigurationValue(string key, string value)
    {
        try
        {
            key = key[5..];
            if (_configuration.SetProperty(key, value))
            {
                return new SetConfigurationResponse { Status = "OK" };
            }
            else
            {
                return new SetConfigurationResponse { Status = "Error" };
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating config value {Key} to {Value}", key, value);
            return new SetConfigurationResponse
            {
                Status = "Error",
                ErrorMessage = ex.Message
            };
        }
    }
}
