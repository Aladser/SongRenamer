using System.ComponentModel;
using System.Linq;
using System.Security;
using System.Text;
using TagLib;

namespace Mp3RenamerV2
{
    public partial class MainFrame : Form
    {
        String text = ""; // �����
        int start = 0, length = 0; // ������ � ����� ��������� �����
        /// <summary>
        /// ������ ����������� ����
        /// </summary>
        List<PainterWord> words = new List<PainterWord>();
        /// <summary>
        /// ���� ��������� ����� ��� �����
        /// </summary>
        private String selectedPath;
        /// <summary>
        /// ���� ��������� ����� ��� �����
        /// </summary>
        private bool isSelectedFile = true; // ���� ������ ����� ��� �����
        private OpenFileDialog openFileDialog;
        private FolderBrowserDialog openFolderDialog;
        private String startPath = "";
        private BackgroundWorker bw; // ������� �����

        public MainFrame()
        {
            InitializeComponent();
            openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "mp3-����� (*.mp3)|*.mp3|flac-����� (*.flac)|*.flac|��� ����� (*.*)|*.*";
            openFolderDialog = new FolderBrowserDialog();
            updateStartPath(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            checkTagsMS.Enabled = false;
        }

        // ������� ������� ����
        private void openFileMenuItem_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                checkStatusLabel.Text = "";
                checkTagsMenuItem.Enabled = true;
                checkNameMenuItem.Enabled = true;

                isSelectedFile = true;
                selectedPath = openFileDialog.FileName;
                print(selectedPath + "\n");
                start = infoField.TextLength;
                String str = showTags(selectedPath);
                print(str);
                length = infoField.TextLength - start;
                if(str.Contains("�����"))
                    words.Add(new PainterWord(start, length, 0));
                else
                    words.Add(new PainterWord(start, length, 1));
                infoField.Text += "\n";
                paintWords();
                updateStartPath(selectedPath.Substring(0, selectedPath.Length - openFileDialog.SafeFileName.Length));   
            }
        }
        // ������� ������� �����
        private void openFolderMenuItem_Click(object sender, EventArgs e)
        {
            if (openFolderDialog.ShowDialog() != DialogResult.OK) return;
            checkStatusLabel.Text = "����������";
            checkTagsMenuItem.Enabled = true;
            checkNameMenuItem.Enabled = true;

            isSelectedFile = false;
            selectedPath = openFolderDialog.SelectedPath;
            print("    ������� �����: " + selectedPath + "\n");
           
            bw = new BackgroundWorker();
            bw.WorkerSupportsCancellation = true;
            bw.WorkerReportsProgress = true;
            bw.ProgressChanged += progressChangedBW;
            bw.RunWorkerCompleted += runCompletedBW;
            bw.DoWork += openFolderBW;
            bw.RunWorkerAsync();
            
        }
        // ������� �����. ��������� ��� �������� ����� � ������ � text
        private void openFolderBW(object sender, DoWorkEventArgs e)
        {
            text = "";
            String[] folderFileElements = Directory.GetFileSystemEntries(selectedPath);// �������� �����
            int progressLength = 2*folderFileElements.Length;
            int progress = 0;
            // ��������� �������� �� �����
            foreach (String elem in folderFileElements)
            {
                if (Path.GetExtension(elem) == "") text += elem+"\n";
                bw.ReportProgress(++progress * 100/ progressLength);
                
            }
            // ��������� ����������� �����
            string ext, str;
            foreach (String elem in folderFileElements)
            {
                ext = Path.GetExtension(elem);
                if (ext==".mp3" || ext==".flac")
                {
                    text += elem + "\n";
                    infoField.Invoke(new Action(() => { start = infoField.TextLength + text.Length; })); // ����� ������ <�����>-<��������>
                    str = showTags(elem) + "\n";
                    text += str;
                    infoField.Invoke(new Action(() => { length = infoField.TextLength + text.Length - start; })); // ����� ������ <�����>-<��������>                    
                    if (str.Contains("�����"))
                        words.Add(new PainterWord(start, length, 0));
                    else
                        words.Add(new PainterWord(start, length, 1));
                }
                bw.ReportProgress(++progress * 100 / progressLength);
            }
            // ���� �������� �����
            String[] foldersInPath = selectedPath.Split("\\"); // ����� ����� � ����
            int rootFolderPathLength = 0;
            for (int i = 0; i < foldersInPath.Length - 1; i++)
                rootFolderPathLength += foldersInPath[i].Length + 1; // � ������ \
            updateStartPath(selectedPath.Substring(0, rootFolderPathLength));
            e.Cancel = true;
        }
       // ������� �������� �����
        private void checkTagsMenuItem_Click(object sender, EventArgs e)
        {
            text = "";
            // �����
            if (isSelectedFile)
            {
                checkStatusLabel.Text = "";
                selectedPath = deleteRedudantSymbols(selectedPath);
                checkTags(selectedPath);
                infoField.Text += text;
                paintWords();
            }
            // �����
            else
            {
                infoField.Text += "    �������� ����� ������\n";
                checkStatusLabel.Text = "����������";
                bw = new BackgroundWorker();
                bw.WorkerSupportsCancellation = true;
                bw.WorkerReportsProgress = true;
                bw.ProgressChanged += progressChangedBW;
                bw.RunWorkerCompleted += runCompletedBW;
                bw.DoWork += checkTagsBW;
                bw.RunWorkerAsync();
                bw.CancelAsync();
            }
        }
        // ������� ������ �������� �����
        private void checkTagsBW(object sender, DoWorkEventArgs e)
        {
            String[] folderFileElements = Directory.GetFileSystemEntries(selectedPath);
            for (int i = 0; i < folderFileElements.Length; i++)
            {
                folderFileElements[i] = deleteRedudantSymbols(folderFileElements[i]);
                checkTags(folderFileElements[i]);
                bw.ReportProgress((i+1)*100/folderFileElements.Length);
            }
            e.Cancel = true;
        }
        // ��������� ������� ����� ������-�������� � ��������� �� �� ����� �����
        private bool checkTags(String file)
        {
            string ext = Path.GetExtension(file);
            if (ext != ".mp3" && ext != ".flac") return false;
            String NameOFTags1 = showTags(file);
            TagLib.File tags = TagLib.File.Create(file);
            // ��������� �������� �����
            if (tags.Tag.Title == null)
            {
                int arg1 = file.LastIndexOf("-") + 2; // ������ ������
                int arg2 = file.Length - arg1 - 4; // ����� �������� ��� ������
                tags.Tag.Title = file.Substring(arg1, arg2); ;
                tags.Save();
            }
            // ��������� �����������
            if (tags.Tag.FirstPerformer == null)
            {
                String fileName = Path.GetFileName(file);
                tags.Tag.Performers = new String[1] { fileName.Substring(0, fileName.LastIndexOf("-") - 1) };
                tags.Save();
            }
            String NameOFTags2 = showTags(file);
            if (!NameOFTags1.Equals(NameOFTags2))
                text += NameOFTags2;
            else
                text += NameOFTags1 + ": ";

            infoField.Invoke(new Action(() => { start = infoField.TextLength + text.Length; })); // ����� ������ <�����>-<��������>
            text += "���������� ����\n";
            infoField.Invoke(new Action(() => { length = infoField.TextLength + text.Length - start; })); // ����� ������ <�����>-<��������>                    
            words.Add(new PainterWord(start, length, 2));
            return true;
        }
        // ������� �������� ����� �����
        private void checkFileNameMenuItem_Click(object sender, EventArgs e)
        {
            selectedPath = checkFileName_Click(selectedPath, false);
        }
        // ������� �������� ����� ����� �������
        private void checkAlbFileNameMenuItem_Click(object sender, EventArgs e)
        {
            selectedPath = checkFileName_Click(selectedPath, true);
        }
        // ��������� � �������� ��� ����� �� ������������ �����
        private string checkFileName_Click(string filename, bool isAlbum)
        {
            text = "";
            if (isSelectedFile)
            {
                string newname = checkFileName(filename, isAlbum);
                if (newname == null) return filename;
                if (!filename.Equals(newname))
                {
                    filename = newname;
                    print(filename + "\n");
                }
                else
                {
                    print(filename + ": ");
                    start = infoField.TextLength + text.Length;
                    print("�������� ����� ������������ �����\n");
                    length = infoField.TextLength + text.Length - start;
                    words.Add(new PainterWord(start, length, 2));
                }
                paintWords();
            }
            else
            {
                infoField.Text += "    �������� ������������ ���� ������ �����\n";
                checkStatusLabel.Text = "����������";

                bw = new BackgroundWorker();
                bw.DoWork += checkFileNameBW;

                bw.WorkerSupportsCancellation = true;
                bw.WorkerReportsProgress = true;
                bw.ProgressChanged += progressChangedBW;
                bw.RunWorkerCompleted += runCompletedBW;

                bw.RunWorkerAsync(isAlbum);
                bw.CancelAsync();
            }
            return filename;
        }
        // ������� ������ �������� ����� �����
        private void checkFileNameBW(object sender, DoWorkEventArgs e)
        {
            text = "";
            String[] folderFileElements = Directory.GetFileSystemEntries(selectedPath);
            string newname, ext;
            for (int i = 0; i < folderFileElements.Length; i++)
            {
                ext = Path.GetExtension(folderFileElements[i]);
                if (ext!=".mp3" && ext!=".flac") continue;
                newname = checkFileName(folderFileElements[i], (bool)e.Argument);
                if (!folderFileElements[i].Equals(newname))
                {
                    folderFileElements[i] = newname;
                    text += folderFileElements[i] + "\n";
                }
                else if(newname == null)
                {
                    text += folderFileElements[i];
                }
                else
                {
                    text += folderFileElements[i] + ": ";
                    infoField.Invoke(new Action(() => { start = infoField.TextLength + text.Length; })); // ����� ������ <�����>-<��������>
                    text += "�������� ������������ �����\n";
                    infoField.Invoke(new Action(() => { length = infoField.TextLength + text.Length - start; })); // ����� ������ <�����>-<��������>                    
                    words.Add(new PainterWord(start, length, 2));
                }
                bw.ReportProgress((i+1) * 100 / folderFileElements.Length);
            }
            e.Cancel = true;
        }
        /// <summary>
        /// ��������� � �������� ��� ����� �� ������������ �����
        /// </summary>
        private string checkFileName(String filename, bool isAlbum)
        {
            string newname = filename.Substring(0, filename.Length - Path.GetFileName(filename).Length);
            TagLib.File tags = TagLib.File.Create(filename);
            // ����������� ������� �����
            if(tags.Tag.Performers == null)
            {
                print("���� ������ ����");
                return null;
            }
            if (tags.Tag.Title == null)
            {
                print("���� ������ ����");
                return null;
            }
            // ������������ ����� ����� ��� ����� �� �������
            if (isAlbum)
            {
                if (tags.Tag.Track < 10) newname += "0" + tags.Tag.Track + ". ";
                else newname += tags.Tag.Track + ". ";
            }
            // ��������� ��� ����� �� �����
            newname += tags.Tag.Performers[0] + " - " + tags.Tag.Title;
            newname += Path.GetExtension(filename) == ".mp3" ? ".mp3" : ".flac";
            {
                 try
                 {
                    System.IO.File.Move(filename, newname);
                 }
                 catch (System.IO.DirectoryNotFoundException exc)
                 {
                    print("�� ���� ������� �������� ����� " + newname + "\n");
                 }
                catch(System.IO.IOException)
                {
                    print("���� ����� ������ ��������� " + newname + "\n");
                    return null;
                }
                  filename = newname;
            }
            newname = deleteRedudantSymbols(filename);
            if (!filename.Equals(newname))
            {
                System.IO.File.Move(filename, newname);
                filename = newname;
            }
            return filename;
        }
        /// <summary>
        /// ���������� �������� ������� ������
        /// </summary>
        private void progressChangedBW(object sender, ProgressChangedEventArgs e)
        {
            progressLabel.Text = (e.ProgressPercentage.ToString() + "%");
            
        }
        /// <summary>
        /// ������� ���������� ������� ������
        /// </summary>
        private void runCompletedBW(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled == true)
            {
                infoField.Text += text;
                paintWords();
                checkStatusLabel.Text = "������";
            }
        }
        /// <summary>
        /// ���������� ���� �����
        /// </summary>
        private String showTags(String file)
        {
            String rslt = "";
            TagLib.File tagSong = TagLib.File.Create(file);
            rslt += "{";
            rslt += tagSong.Tag.FirstPerformer == null ? "�����" : tagSong.Tag.FirstPerformer;
            rslt += "} - {";
            rslt += tagSong.Tag.Title == null ? "�����" : tagSong.Tag.Title;
            rslt += "}";
            return rslt;
        }
        /// <summary>
        /// ��������� ����� �������� openFileDialog
        /// </summary>
        private void updateStartPath(String value)
        {
            startPath = value;
            openFileDialog.InitialDirectory = startPath;
            openFolderDialog.InitialDirectory = startPath;
        }
        /// <summary>
        /// ������� ���������� � infoField
        /// </summary>
        private void print(String text)
        {
            infoField.Invoke(new Action(() => { infoField.Text += text; }));
        }
        /// <summary>
        /// ������� ������ ������� �� ��������
        /// </summary>
        private string deleteRedudantSymbols(string filename)
        {
            String newName;
            // ������� �� ����� ����� -kissvk.com
            String kissVK = "-kissvk.com";
            if (filename.Contains(kissVK))
            {
                newName = filename.Remove(filename.IndexOf(kissVK), kissVK.Length);
                System.IO.File.Move(filename, newName);
                filename = newName;
            }
            // ����������� " - "
            if (filename.Contains("-") && !filename.Contains(" - "))
            {
                {
                    newName = filename.Replace("-", " - ");
                    System.IO.File.Move(filename, newName);
                    filename = newName;
                }
            }
            return filename;
        }
        /// <summary>
        /// ������� infoField
        /// </summary>
        private void clearInfoField_Click(object sender, EventArgs e)
        {
            infoField.Text = "";
            words = new List<PainterWord>();

        }
        ///
        /// ����� ����������� �����
        /// 
        private struct PainterWord {
            public int start;
            public int length;
            public int type;
            public PainterWord(int start, int length, int type)
            {
                this.start = start;
                this.length = length;
                this.type = type;
            }
        }

        private void paintWords()
        {
            for (int i = 0; i < words.Count; i++)
            {
                infoField.SelectionStart = words[i].start;
                infoField.SelectionLength = words[i].length;
                switch (words[i].type)
                {
                    case 0:
                        infoField.SelectionColor = Color.Red;
                        break;
                    case 1:
                        infoField.SelectionColor = Color.Blue;
                        break;
                    default:
                        infoField.SelectionColor= Color.Green;
                        break;
                }
            }
            // ����������� ����������� ����
            infoField.SelectionStart = infoField.TextLength;
            infoField.SelectionLength = 0;
        }

    }
}