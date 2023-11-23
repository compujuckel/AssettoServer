using FluentValidation;

namespace AssettoServer.Server.Configuration;

public interface IValidateConfiguration<T> where T : IValidator;
