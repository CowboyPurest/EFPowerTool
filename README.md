# Experimental branch for EF power tool 
Contains a modified branch of EF(6.1.3) Power Tool.

This branch have few additional features developed to make the tool more handy. 
#### 1. Custom directory generation
1. Removed the hard coded value of `Model` and `Mapping` namespaces. Instead they will be used as default namespaces if there's no custom namespace spcified in code template files.
2. Allows new namespace to be specified via code templates files.
```csharp
      // Customize the file generations according to namespace hierarchy
      efHost.Namespace = "DataAccess";
      efHost.ModelsNamespace = "DataAccess.Models";
      efHost.MappingNamespace = "DataAccess.Mappings";
```
The directory structure according to above setting would be generated like below:

![Directory structure](http://i.stack.imgur.com/Yvnc4.png)

See details here - [Cutom directory heirarchy code generation with custom defined namespace](http://www.cshandler.com/2016/03/customize-reverse-engineer-code-first.html)

Issue reported on codeplex: http://entityframework.codeplex.com/workitem/2892

#### 2. Re-use of existing connections when running "Reverse enginner code templates" command
This change will prompt existing connection string to use instead of creating the connection again via VS Database connection tool. 

![Connection prompt](http://i.stack.imgur.com/yl2l9.png)

1. Allows to select connection from existing ones.
2. Allows to create new connection using `[..]` button.

-------------------------
**Status** - Ready for testing. 

If you want to test the new changes proposed in the issue above. Download the .vsix from install directory from source. 

Caution -  This is experimental branch for testing purpose only. 

Project original source - http://entityframework.codeplex.com/
