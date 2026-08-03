using L2Dn.ClientDat;

try
{
    return args.FirstOrDefault()?.ToLowerInvariant() switch
    {
        "inspect" => Commands.Inspect(args),
        "build-special-craft" => Commands.BuildSpecialCraft(args),
        "verify-special-craft" => Commands.VerifySpecialCraft(args),
        _ => Commands.Usage(),
    };
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
