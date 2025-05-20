namespace ebuild.api.exceptions{
    public class ModuleConstructorNotFoundException : Exception
    {
        public ModuleConstructorNotFoundException(Type classInModule):
            base($"Module constructor not found for {classInModule.Name}")
        {
        }
    }

    public class ModuleConstructionFailedException : Exception
    {
        public ModuleConstructionFailedException(Type classInModule):
            base($"Module construction failed for {classInModule.Name}")
        {
        }
    }


    public class ModuleFileNotFoundException : Exception
    {
        public ModuleFileNotFoundException(string moduleName):
            base($"Module file {moduleName} not found")
        {
        }
    }

    public class ModuleFileCompilationFailedException : Exception
    {
        public ModuleFileCompilationFailedException(string moduleName):
            base($"Module file compilation failed for {moduleName}")
        {
        }
    }
}