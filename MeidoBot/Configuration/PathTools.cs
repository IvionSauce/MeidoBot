using System;
using System.IO;


namespace MeidoBot
{
    static class PathTools
    {
        public static bool CheckPluginIO(MeidoConfig conf, Logger log)
        {
            return
                VerifyDirectory(conf.ConfigurationDirectory, log) &&
                VerifyDirectory(conf.DataDirectory, log) &&
                RwTest(conf.DataDirectory, log);
        }

        public static bool CheckChatlogIO(string chatlogDir, Logger log)
        {
            return
                VerifyDirectory(chatlogDir, log) &&
                RwTest(chatlogDir, log);
        }


        static bool VerifyDirectory(string directory, Logger log)
        {
            try
            {
                Directory.CreateDirectory(directory);
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


        static bool RwTest(string directory, Logger log)
        {
            FileStream stream = null;
            var dataTestPath = Path.Combine(directory, "rw-test");
            try
            {
                stream = File.Create(dataTestPath);
                stream.Dispose();
                File.Delete(dataTestPath);

                return true;
            }
            catch (IOException ex)
            {
                log.Error("IO Exception in testing r/w of '{0}': {1}", directory, ex.Message);
            }
            catch (AccessViolationException)
            {
                log.Error("Access Violation in testing r/w of '{0}'.", directory);
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