using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Net.Http;
using System.Linq;
using System.Threading.Tasks;

var html = await Load("https://he.wikipedia.org/wiki/%D7%A7%D7%95%D7%93_%D7%9E%D7%A7%D7%95%D7%A8");

var cleanhtml = new Regex("\\s").Replace(html, " ");
var htmlLines = new Regex("<(.*?)>").Split(cleanhtml).Where(s => s.Length > 0);

var htmlHelper = HtmlHelper.GetInstance("HtmlTags.json", "HtmlVoidTags.json");

HtmlElement root = new HtmlElement("html");
HtmlElement currentElement = root;

foreach (var line in htmlLines)
{
    string[] words = line.Split(' ');
    string firstWord = words[0];

    if (firstWord == "html/")
    {
        break;
    }
    else if (firstWord.StartsWith("/"))
    {
        currentElement = currentElement.Parent;
    }
    else
    {
        string tagName = firstWord.TrimStart('<').TrimEnd('>');
        HtmlElement newElement = new HtmlElement(tagName);

        if (htmlHelper.IsValidTag(tagName))
        {
            var classAttr = words.FirstOrDefault(w => w.StartsWith("class="));
            if (!string.IsNullOrEmpty(classAttr))
            {
                var classValue = classAttr.Split('=')[1].Trim('"');
                foreach (var className in classValue.Split(' '))
                {
                    newElement.AddClass(className);
                }
            }

            newElement.Name = tagName;
            var idAttr = words.FirstOrDefault(w => w.StartsWith("id="));
            if (!string.IsNullOrEmpty(idAttr))
            {
                newElement.Id = idAttr.Split('=')[1].Trim('"');
            }

            currentElement.AddChild(newElement);

            if (htmlHelper.IsSelfClosingTag(tagName) || line.Trim().EndsWith("/>"))
            {
                continue;
            }

            currentElement = newElement;
            string remainingLine = string.Join(" ", words.Skip(1));
            var attributes = new Regex("([^\\s]*?)=\"(.*?)\"").Matches(remainingLine);

            foreach (Match match in attributes)
            {
                newElement.AddAttribute(match.Groups[1].Value, match.Groups[2].Value);
            }
        }
        else
        {
            currentElement.InnerHtml = line;
        }
    }
}

// ����� �� ����� ��������� ����� ���� ���� ���
Console.WriteLine("Searching for elements with tag 'div':");
var divElements = root.FindElementsByTagName("div");
foreach (var div in divElements)
{
    Console.WriteLine($"Found div with ID: {div.Id} and Classes: {string.Join(", ", div.Classes)}");
}

// ����� ����� ��������� ����� �����
Console.WriteLine("\nSearching for elements with class 'main':");
var classElements = root.FindElementsByClassName("main");
foreach (var element in classElements)
{
    Console.WriteLine($"Found element: {element.Name}");
}

// ���� ��� ����
Console.WriteLine("\nFull tree:");
Console.WriteLine(root.Render());

Console.ReadLine();

async Task<string> Load(string url)
{
    HttpClient client = new HttpClient();
    var response = await client.GetAsync(url);
    var html = await response.Content.ReadAsStringAsync();
    return html;
}

public class HtmlElement
{
    public string Id { get; set; }
    public string Name { get; set; }
    public Dictionary<string, string> Attributes { get; set; }
    public List<string> Classes { get; set; }
    public string InnerHtml { get; set; }

    public HtmlElement Parent { get; set; }
    public List<HtmlElement> Children { get; set; }

    public HtmlElement(string name)
    {
        Name = name;
        Attributes = new Dictionary<string, string>();
        Classes = new List<string>();
        Children = new List<HtmlElement>();
    }

    public void AddAttribute(string key, string value)
    {
        Attributes[key] = value;
    }

    public void AddClass(string className)
    {
        if (!Classes.Contains(className))
        {
            Classes.Add(className);
        }
    }

    public void AddChild(HtmlElement child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    public string Render(int indentLevel = 0)
    {
        string indent = new string(' ', indentLevel * 2);
        string classes = Classes.Count > 0 ? $" class=\"{string.Join(" ", Classes)}\"" : "";
        string attributes = "";
        foreach (var attr in Attributes)
        {
            attributes += $" {attr.Key}=\"{attr.Value}\"";
        }

        string openTag = $"{indent}<{Name}{classes}{attributes}>";
        string closeTag = $"{indent}</{Name}>";
        string innerHtml = InnerHtml;

        foreach (var child in Children)
        {
            innerHtml += "\n" + child.Render(indentLevel + 1);
        }

        return $"{openTag}{(innerHtml != null ? "\n" + innerHtml + "\n" : "")}{closeTag}";
    }

    public List<HtmlElement> FindElementsByTagName(string tagName)
    {
        var result = new List<HtmlElement>();

        if (Name.Equals(tagName, StringComparison.OrdinalIgnoreCase))
        {
            result.Add(this);
        }

        foreach (var child in Children)
        {
            result.AddRange(child.FindElementsByTagName(tagName));
        }

        return result;
    }

    public List<HtmlElement> FindElementsById(string id)
    {
        var result = new List<HtmlElement>();

        if (Id != null && Id.Equals(id, StringComparison.OrdinalIgnoreCase))
        {
            result.Add(this);
        }

        foreach (var child in Children)
        {
            result.AddRange(child.FindElementsById(id));
        }

        return result;
    }

    public List<HtmlElement> FindElementsByClassName(string className)
    {
        var result = new List<HtmlElement>();

        if (Classes.Contains(className))
        {
            result.Add(this);
        }

        foreach (var child in Children)
        {
            result.AddRange(child.FindElementsByClassName(className));
        }

        return result;
    }
}

public class HtmlHelper
{
    private static HtmlHelper _instance;
    private static readonly object _lock = new object();

    public string[] HtmlTags { get; private set; }
    public string[] HtmlVoidTags { get; private set; }

    private HtmlHelper(string allTagsPath, string selfClosingTagsPath)
    {
        HtmlTags = LoadTagsFromFile(allTagsPath);
        HtmlVoidTags = LoadTagsFromFile(selfClosingTagsPath);
    }

    public static HtmlHelper GetInstance(string allTagsPath, string selfClosingTagsPath)
    {
        if (_instance == null)
        {
            lock (_lock)
            {
                if (_instance == null)
                {
                    _instance = new HtmlHelper(allTagsPath, selfClosingTagsPath);
                }
            }
        }

        return _instance;
    }

    private string[] LoadTagsFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"The file '{filePath}' does not exist.");
        }

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
    }

    public bool IsValidTag(string tagName)
    {
        return Array.Exists(HtmlTags, tag => tag.Equals(tagName, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsSelfClosingTag(string tagName)
    {
        return Array.Exists(HtmlVoidTags, tag => tag.Equals(tagName, StringComparison.OrdinalIgnoreCase));
    }
}
