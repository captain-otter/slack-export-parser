using Godot;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

public partial class App : Control
{
	[Export] private Button selectFileButton;
	[Export] private FileDialog fileSelectDialog;
	[Export] private Label progressLabel;
	[Export] private ProgressBar progressBar;
	[Export] private RichTextLabel outputLabel;
	[Export] private RichTextLabel skippedOutputLabel;
	[Export] private Slider delaySlider;
	[Export] private Label delayLabel;

	private string workingDirectory = "";

	StringBuilder outputString = new StringBuilder();
	StringBuilder skippedFilesString = new StringBuilder();

	private struct FileDetails
	{
		public string JsonFileName;
		public string FileName;
		public string Channel;
		public string FileUrl;
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		fileSelectDialog.FileSelected += (string file) =>
			{
				StartParse(file);
			};

		selectFileButton.Pressed += () =>
		{
			fileSelectDialog.Show();
		};

		delaySlider.ValueChanged += (double newValue) =>
		{
			delayLabel.Text = $"Delay {newValue}ms";
		};

		delayLabel.Text = $"Delay {delaySlider.Value}ms";
	}

	public async void StartParse(string channelsFile)
	{
		workingDirectory = System.IO.Path.GetDirectoryName(channelsFile);

		List<FileDetails> filesToGrab = GetFilesToGrab(channelsFile);

		await GrabFiles(filesToGrab);

		AppendToOutput($"FINISHED");
	}

	private List<FileDetails> GetFilesToGrab(string channelsFile)
	{
		List<FileDetails> filesToGrab = new List<FileDetails>();

		List<SlackChannel> slackChannels = GetAllChannels(channelsFile);
		foreach (SlackChannel channel in slackChannels)
		{
			string channelFolderLocation = System.IO.Path.Combine(workingDirectory, channel.Name);
			AppendToOutput($"Parsing channel: {channel.Name}");

			System.IO.DirectoryInfo dir = new System.IO.DirectoryInfo(channelFolderLocation);

			System.IO.FileInfo[] jsonFiles = dir.GetFiles("*.json"); //Get JSON files

			foreach (System.IO.FileInfo jsonFile in jsonFiles)
			{
				List<SlackMessage> messages = LoadMessagesFrom(jsonFile.FullName);

				if (messages != null && messages.Count > 0)
				{
					foreach (SlackMessage message in messages)
					{
						if (message.SlackFiles != null && message.SlackFiles.Count > 0)
						{
							foreach (SlackFile slackFile in message.SlackFiles)
							{
								filesToGrab.Add(new FileDetails() { FileName = slackFile.Name, Channel = channel.Name, FileUrl = slackFile.DownloadUrl, JsonFileName = jsonFile.Name });
							}
						}
					}
				}
				;
			}
		}
		return filesToGrab;
	}

	private async Task GrabFiles(List<FileDetails> filesToGrab)
	{
		int filesToGrabCount = filesToGrab.Count;
		int parsedFiles = 0;

		AppendToOutput($"Files to parse: {filesToGrabCount}");

		int msDelay = (int)delaySlider.Value;

		foreach (FileDetails item in filesToGrab)
		{
			AppendToOutput($"PARSING - JSON: {item.JsonFileName}, Channel: {item.Channel} Filename: {item.FileName}\nFileUrl: {item.FileUrl}\n");

			try
			{
				await FetchAndSaveImageFromUrl(item.Channel, item.FileName, item.FileUrl);
				await Task.Delay(msDelay);
			}
			catch (System.Exception err)
			{
				AppendToSkippedOutput($"SKIPPED - JSON: {item.JsonFileName}, Channel: {item.Channel} Filename: {item.FileName}\nFileUrl: {item.FileUrl}\nError: {err}\n\n");
				skippedOutputLabel.Text = skippedFilesString.ToString();
				continue;
			}

			parsedFiles++;
			progressLabel.Text = $"{parsedFiles}/{filesToGrabCount}";
			progressBar.Value = (float)parsedFiles / (float)filesToGrabCount;
		}
	}

	private void AppendToOutput(string text)
	{
		outputString.AppendLine(text);
		outputLabel.Text = outputString.ToString();
	}

	private void AppendToSkippedOutput(string text)
	{
		skippedFilesString.AppendLine(text);
		skippedOutputLabel.Text = skippedFilesString.ToString();
	}

	private async Task FetchAndSaveImageFromUrl(string channelName, string desiredFileName, string url)
	{
		System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
		var response = await client.GetAsync(url);
		response.EnsureSuccessStatusCode();
		var stream = response.Content.ReadAsStream();

		string fileName = $"{System.DateTime.Now.ToFileTime()}_{ReplaceInvalidChars(desiredFileName)}";
		string outputDirectory = System.IO.Path.Combine(workingDirectory, "export", channelName);
		System.IO.Directory.CreateDirectory(outputDirectory);

		string filePath = System.IO.Path.Combine(outputDirectory, fileName);

		using System.IO.Stream output = System.IO.File.OpenWrite(filePath);
		using System.IO.Stream input = stream;
		input.CopyTo(output);
	}

	public List<SlackChannel> GetAllChannels(string filePath)
	{
		if (!FileAccess.FileExists(filePath))
		{
			GD.Print("File does not exist!");
			return null;
		}

		using FileAccess file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
		string jsonString = file.GetAsText();
		return JsonConvert.DeserializeObject<List<SlackChannel>>(jsonString);
	}

	public List<SlackMessage> LoadMessagesFrom(string filePath)
	{
		if (!FileAccess.FileExists(filePath))
		{
			return null;
		}

		using FileAccess file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
		string jsonString = file.GetAsText();
		return JsonConvert.DeserializeObject<List<SlackMessage>>(jsonString);
	}

	public static string ReplaceInvalidChars(string filename)
	{
		return string.Join("_", filename.Split(System.IO.Path.GetInvalidFileNameChars()));
	}
}
