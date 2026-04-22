using ApixPress.Updater;

try
{
    return await UpdateRunner.RunAsync(args);
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
