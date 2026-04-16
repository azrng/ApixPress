using GeneralUpdate.Core;

try
{
    await GeneralUpdateOSS.Start();
}
catch (Exception exception)
{
    Console.WriteLine(exception.Message);
}
