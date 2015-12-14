using System;
using System.IO;


namespace MeidoBot
{
    static class PathTools
    {
        public static bool CheckIO(MeidoConfig conf, Logger log)
        {
            return VerifyPaths(conf, log) && DataRwTest(conf.DataDirectory, log);
        }

        static bool VerifyPaths(MeidoConfig conf, Logger log)
        {
            try
            {
                Directory.CreateDirectory(conf.ConfigurationDirectory);
                Directory.CreateDirectory(conf.DataDirectory);
                return true;
            }
            catch (IOException ex)
            {
                log.Error("IO Exception in verifying paths: " + ex.Message);
            }
            catch (AccessViolationException ex)
            {
                log.Error("Access Violation in verifying paths: " + ex.Message);
            }

            return false;
        }

        static bool DataRwTest(string dataPath, Logger log)
        {
            FileStream stream = null;
            var dataTestPath = Path.Combine(dataPath, "rw-test");
            try
            {
                stream = File.Create(dataTestPath);
                stream.Dispose();
                File.Delete(dataTestPath);

                return true;
            }
            catch (IOException ex)
            {
                log.Error("IO Exception in testing r/w of '{0}': {1}", dataPath, ex.Message);
            }
            catch (AccessViolationException)
            {
                log.Error("Access Violation in testing r/w of '{0}'.", dataPath);
            }
            finally
            {
                if (stream != null)
                    stream.Dispose();
            }

            return false;
        }

    }
}