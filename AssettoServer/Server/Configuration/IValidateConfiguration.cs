using FluentValidation;
using JetBrains.Annotations;

namespace AssettoServer.Server.Configuration;

public interface IValidateConfiguration<[MeansImplicitUse] T> where T : IValidator;
