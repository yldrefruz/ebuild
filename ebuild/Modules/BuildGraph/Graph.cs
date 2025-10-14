using ebuild.api;

namespace ebuild.Modules.BuildGraph;


public class Graph(ModuleBase module)
{
    public ModuleBase Module { get; init; } = module;
    private Node RootNode = new ModuleDeclarationNode(module);

    // Cache for circular dependency detection results
    private bool? _hasCircularDependencyCache = null;
    private List<Node>? _circularDependencyPathCache = null;

    public Node GetRootNode() => RootNode;
    public T CreateWorker<T>() where T : class, IWorker => CreateWorker(typeof(T)) as T ?? throw new Exception($"Failed to create worker of type {typeof(T).FullName}");
    public IWorker CreateWorker(Type WorkerType) => (IWorker)Activator.CreateInstance(WorkerType, this)!;


    /// <summary>
    /// Checks if the build graph has any circular dependencies
    /// </summary>
    /// <returns>True if circular dependencies exist, false otherwise</returns>
    public bool HasCircularDependency()
    {
        if (_hasCircularDependencyCache.HasValue)
        {
            return _hasCircularDependencyCache.Value;
        }

        var cycle = GetCircularDependencyPath();
        _hasCircularDependencyCache = cycle.Count > 0;
        return _hasCircularDependencyCache.Value;
    }

    /// <summary>
    /// Gets the path of nodes forming a circular dependency
    /// </summary>
    /// <returns>List of nodes in the circular dependency path, or empty list if none found</returns>
    public List<Node> GetCircularDependencyPath()
    {
        if (_circularDependencyPathCache != null)
        {
            return _circularDependencyPathCache;
        }

        _circularDependencyPathCache = RootNode.DetectCircularDependency();
        return _circularDependencyPathCache;
    }

    /// <summary>
    /// Gets a formatted string representation of the circular dependency path
    /// </summary>
    /// <returns>String describing the circular dependency, or empty string if none found</returns>
    public string GetCircularDependencyPathString()
    {
        var cycle = GetCircularDependencyPath();
        if (cycle.Count == 0)
        {
            return string.Empty;
        }

        var pathBuilder = new System.Text.StringBuilder();
        pathBuilder.AppendLine("Circular dependency detected:");

        for (int i = 0; i < cycle.Count; i++)
        {
            var node = cycle[i];
            if (node is ModuleDeclarationNode moduleNode)
            {
                pathBuilder.Append($"  {moduleNode.Module.Name}");
                if (i < cycle.Count - 1)
                {
                    pathBuilder.Append(" -> ");
                }
            }
        }

        // Close the cycle by showing it returns to the first module
        if (cycle.Count > 0 && cycle[0] is ModuleDeclarationNode firstModule)
        {
            pathBuilder.Append($" -> {firstModule.Module.Name}");
        }

        return pathBuilder.ToString();
    }


    public string CreateTreeString()
    {
        var sb = new System.Text.StringBuilder();
        // create a tree string representation of the graph
        void AppendNodeString(Node node, string indent, bool isLast)
        {
            sb.Append(indent);
            if (isLast)
            {
                sb.Append("`-");
                indent += "  ";
            }
            else
            {
                sb.Append("|-");
                indent += "| ";
            }
            sb.AppendLine(node.Name);

            var children = node.Children.Joined();
            for (int i = 0; i < children.Count; i++)
            {
                AppendNodeString(children[i], indent, i == children.Count - 1);
            }
        }
        AppendNodeString(RootNode, "", true);
        return sb.ToString();
    }

    public string CreateTreeHtml()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(
"""
<!DOCTYPE html>
<html lang="en">

<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Build Graph</title>
    <style>
        body {
            font-family: monospace;
            background-color: #111111;
            color: whitesmoke;
        }

        .node {
            margin: 20px 20px;
            padding: 8px;
            border: 1px solid #444;
            border-radius: 8px;
        }

        .ModuleDeclarationNode {
            background-color: #010511;
        }
        .CompileSourceFileNode {
            background-color: #1a1800;
        }
        .LinkNode {
            background-color: #1a0022;
        }
    </style>
    <script>
        function toggleChildren(event) {
            const node = event.currentTarget;
            const children = node.querySelectorAll(':scope > .node');
            children.forEach(child => {
                if (child.style.display === 'none') {
                    child.style.display = 'block';
                } else {
                    child.style.display = 'none';
                }
            });
            event.stopPropagation();
        }

        function addToggleListeners() {
            const nodes = document.querySelectorAll('.node > p');
            nodes.forEach(node => {
                node.style.cursor = 'pointer';
                node.addEventListener('click', toggleChildren);
            });
        }
        document.addEventListener('DOMContentLoaded', () => {
            addToggleListeners();
            // add hide checkboxes below Build Graph title 
            // we need one for each node type: ModuleDeclarationNode, CompileSourceFileNode, LinkNode, BuildStepNode
            const title = document.querySelector('h1');
            const types = ['ModuleDeclarationNode', 'CompileSourceFileNode', 'LinkerNode', 'BuildStepNode'];
            // Create a container div for checkboxes
            const checkboxContainer = document.createElement('div');
            checkboxContainer.style.margin = '10px 0';
            types.forEach(type => {
                const label = document.createElement('label');
                label.style.marginLeft = '10px';
                const checkbox = document.createElement('input');
                checkbox.type = 'checkbox';
                checkbox.checked = true;
                checkbox.addEventListener('change', (event) => {
                    const checked = event.target.checked;
                    const nodes = document.querySelectorAll(`.${type}`);
                    nodes.forEach(node => {
                        node.style.display = checked ? 'block' : 'none';
                    });
                });
                label.appendChild(checkbox);
                label.appendChild(document.createTextNode(` Show ${type}`));
                checkboxContainer.appendChild(label);
            });
            // Insert the container after the title
            title.parentNode.insertBefore(checkboxContainer, title.nextSibling);

        });


    </script>
</head>
<body>
<h1>Build Graph</h1>
"""
        );

        void AppendNodeHtml(Node node, int level)
        {
            var indent = new string(' ', level * 4);
            var inner_indent = new string(' ', (level + 1) * 4);
            sb.AppendLine($"{indent}<div class=\"node {node.GetType().Name}\">");
            sb.AppendLine($"{inner_indent}<p>{node.Name}</p>");

            foreach (var child in node.Children.Joined())
            {
                AppendNodeHtml(child, level + 1);
            }
            sb.AppendLine($"{indent}</div>");
        }

        AppendNodeHtml(RootNode, 0);

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
}