#region Copyright (C) 2005-2011 Team MediaPortal

// Copyright (C) 2005-2011 Team MediaPortal
// http://www.team-mediaportal.com
// 
// MediaPortal is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// MediaPortal is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MediaPortal. If not, see <http://www.gnu.org/licenses/>.

#endregion

using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using MediaPortal.GUI.Library;
using MediaPortal.UserInterface.Controls;
using Action = MediaPortal.GUI.Library.Action;

#pragma warning disable 108

namespace MediaPortal.Configuration.Sections
{
  public class Keys : SectionSettings
  {
    private MPGroupBox groupBox1;
    private MPLabel label1;
    private MPButton deleteButton;
    private MPButton addButton;
    private MPLabel label2;
    private MPLabel label3;
    private MPTextBox keyTextBox;
    private MPTextBox soundTextBox;
    private MPLabel label4;
    protected MPButton fileNameButton;
    private IContainer components = null;

    //
    // Private members
    //
    private ArrayList globalActions = new ArrayList();
    private TreeView keyTreeView;
    private MPTextBox descriptionTextBox;
    private OpenFileDialog openFileDialog;
    private ArrayList windowActions = new ArrayList();

    private TreeNode currentlySelectedNode;
    private TreeNode globalNode, windowsNode;

    private KeyMappings keyMappings = new KeyMappings();
    private MPComboBox idComboBox;
    private MPTextBox idTextBox;

    public Keys()
      : this("Keys and Sounds") {}

    public Keys(string name)
      : base(name)
    {
      // This call is required by the Windows Form Designer.
      InitializeComponent();

      // Load the keys
      LoadKeys();

      // Populate the tree
      PopulateKeyTree();

      // Fill action combo
      string[] names = Enum.GetNames(typeof (Action.ActionType));
      idComboBox.Items.AddRange(names);
    }

    private void PopulateKeyTree()
    {
      globalNode = new TreeNode("Global actions");
      keyTreeView.Nodes.Add(globalNode);

      //
      // Populate node with actions
      //
      AddActionsToNode(ref globalNode, globalActions);

      windowsNode = new TreeNode("Windows");
      keyTreeView.Nodes.Add(windowsNode);

      //
      // Populate node with actions
      //
      foreach (ActionWindow actionWindow in windowActions)
      {
        TreeNode windowNode = new TreeNode(actionWindow.Description + " (" + actionWindow.Id + ")");

        AddActionsToNode(ref windowNode, actionWindow.Actions);

        windowNode.Tag = actionWindow;

        windowsNode.Nodes.Add(windowNode);
      }
    }

    private string GetActionName(int id)
    {
      string action = Strings.Unknown;

      try
      {
        action = Enum.GetName(typeof (Action.ActionType), id);
      }
      catch {}

      return action;
    }

    private void AddActionsToNode(ref TreeNode treeNode, ArrayList actions)
    {
      foreach (KeyAction action in actions)
      {
        TreeNode actionNode = new TreeNode(action.Description + " (" + action.Id + ")");

        actionNode.Nodes.Add("Action = " + GetActionName(action.Id));
        actionNode.Nodes.Add("Key = " + action.Key);
        actionNode.Nodes.Add("Sound = " + action.Sound);

        actionNode.Tag = action;

        treeNode.Nodes.Add(actionNode);
      }
    }

    private void SaveKeys()
    {
      try
      {
        using (FileStream fileStream = new FileStream(Config.GetFile(Config.Dir.Config, "keymap.xml"), FileMode.Create))
        {
          using (XmlTextWriter writer = new XmlTextWriter(fileStream, Encoding.UTF8))
          {
            writer.Formatting = Formatting.Indented;

            writer.WriteStartDocument();
            writer.WriteComment("This document was auto-generated by MediaPortal Settings program.");
            //writer.WriteComment("Version = " + System.Windows.Forms.Application.ProductVersion);

            writer.WriteStartElement("keymap");
            writer.WriteStartElement("global");

            foreach (KeyAction action in globalActions)
            {
              writer.WriteStartElement("action");

              writer.WriteStartElement("id");
              writer.WriteString(action.Id.ToString());
              writer.WriteEndElement();

              writer.WriteStartElement("key");
              writer.WriteString(action.Key);
              writer.WriteEndElement();

              writer.WriteStartElement("sound");
              writer.WriteString(action.Sound);
              writer.WriteEndElement();

              writer.WriteStartElement("description");
              writer.WriteString(action.Description);
              writer.WriteEndElement();

              writer.WriteEndElement();
            }
            writer.WriteEndElement();

            foreach (ActionWindow window in windowActions)
            {
              writer.WriteStartElement("window");

              writer.WriteStartElement("id");
              writer.WriteString(window.Id.ToString());
              writer.WriteEndElement();

              writer.WriteStartElement("description");
              writer.WriteString(window.Description);
              writer.WriteEndElement();

              foreach (KeyAction action in window.Actions)
              {
                writer.WriteStartElement("action");

                writer.WriteStartElement("id");
                writer.WriteString(action.Id.ToString());
                writer.WriteEndElement();

                writer.WriteStartElement("key");
                writer.WriteString(action.Key);
                writer.WriteEndElement();

                writer.WriteStartElement("sound");
                writer.WriteString(action.Sound);
                writer.WriteEndElement();

                writer.WriteStartElement("description");
                writer.WriteString(action.Description);
                writer.WriteEndElement();

                writer.WriteEndElement();
              }
              writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndDocument();
          }
        }
      }
      catch (Exception ex)
      {
        Log.Error("GeneralKeys: Error writing Keymap.xml - {0}", ex.Message);
      }
    }

    public override void SaveSettings()
    {
      SaveKeys();
    }

    private void LoadKeys()
    {
      XmlDocument document = new XmlDocument();

      try
      {
        // Load the xml document
        string keyConfigFile = Config.GetFile(Config.Dir.Config, "keymap.xml");
        if (!File.Exists(keyConfigFile))
        {
          keyConfigFile = Config.GetFile(Config.Dir.Base, "keymap.xml");
        }

        if (File.Exists(keyConfigFile))
        {
          document.Load(keyConfigFile);
        }
        else
        {
          Log.Error(
            "GeneralKeys: Error loading Keymap.xml - make sure you've got a valid file in your base or config directory!");
          return;
        }

        XmlElement rootElement = document.DocumentElement;
        // Make sure we're loading a keymap
        if (rootElement != null && rootElement.Name.Equals("keymap"))
        {
          // Fetch global actions
          XmlNodeList nodeList = rootElement.SelectNodes("/keymap/global/action");

          foreach (XmlNode node in nodeList)
          {
            AddActionNode(ref globalActions, node);
          }
          // Fetch all windows
          nodeList = rootElement.SelectNodes("/keymap/window");

          foreach (XmlNode node in nodeList)
          {
            // Allocate new window
            ActionWindow window = new ActionWindow();
            // Fetch description and id for this window
            XmlNode idNode = node.SelectSingleNode("id");
            XmlNode descriptionNode = node.SelectSingleNode("description");

            window.Description = descriptionNode.InnerText;
            window.Id = Convert.ToInt32(idNode.InnerText.Length > 0 ? idNode.InnerText : "0");

            XmlNodeList actionNodeList = node.SelectNodes("action");

            foreach (XmlNode actionNode in actionNodeList)
            {
              AddActionNode(ref window.Actions, actionNode);
            }
            // Add to the window list
            windowActions.Add(window);
          }
        }
      }
      catch (Exception e)
      {
        Debug.WriteLine(e.Message);
      }
    }

    private void AddActionNode(ref ArrayList list, XmlNode node)
    {
      string id = "0", key = string.Empty, sound = string.Empty, description = string.Empty;

      XmlNode idNode = node.SelectSingleNode("id");
      XmlNode keyNode = node.SelectSingleNode("key");
      XmlNode soundNode = node.SelectSingleNode("sound");
      XmlNode descriptionNode = node.SelectSingleNode("description");

      if (idNode != null)
      {
        id = idNode.InnerText;
      }

      if (keyNode != null)
      {
        key = keyNode.InnerText;
      }

      if (soundNode != null)
      {
        sound = soundNode.InnerText;
      }

      if (descriptionNode != null)
      {
        description = descriptionNode.InnerText;
      }

      list.Add(new KeyAction(Convert.ToInt32(id), key, description, sound));
    }

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
      if (disposing)
      {
        if (components != null)
        {
          components.Dispose();
        }
      }
      base.Dispose(disposing);
    }

    #region Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
      this.groupBox1 = new MediaPortal.UserInterface.Controls.MPGroupBox();
      this.idTextBox = new MediaPortal.UserInterface.Controls.MPTextBox();
      this.idComboBox = new MediaPortal.UserInterface.Controls.MPComboBox();
      this.addButton = new MediaPortal.UserInterface.Controls.MPButton();
      this.deleteButton = new MediaPortal.UserInterface.Controls.MPButton();
      this.fileNameButton = new MediaPortal.UserInterface.Controls.MPButton();
      this.soundTextBox = new MediaPortal.UserInterface.Controls.MPTextBox();
      this.label4 = new MediaPortal.UserInterface.Controls.MPLabel();
      this.keyTextBox = new MediaPortal.UserInterface.Controls.MPTextBox();
      this.label3 = new MediaPortal.UserInterface.Controls.MPLabel();
      this.label2 = new MediaPortal.UserInterface.Controls.MPLabel();
      this.descriptionTextBox = new MediaPortal.UserInterface.Controls.MPTextBox();
      this.label1 = new MediaPortal.UserInterface.Controls.MPLabel();
      this.keyTreeView = new System.Windows.Forms.TreeView();
      this.openFileDialog = new System.Windows.Forms.OpenFileDialog();
      this.groupBox1.SuspendLayout();
      this.SuspendLayout();
      // 
      // groupBox1
      // 
      this.groupBox1.Anchor =
        ((System.Windows.Forms.AnchorStyles)
         ((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
           | System.Windows.Forms.AnchorStyles.Right)));
      this.groupBox1.Controls.Add(this.idTextBox);
      this.groupBox1.Controls.Add(this.idComboBox);
      this.groupBox1.Controls.Add(this.addButton);
      this.groupBox1.Controls.Add(this.deleteButton);
      this.groupBox1.Controls.Add(this.fileNameButton);
      this.groupBox1.Controls.Add(this.soundTextBox);
      this.groupBox1.Controls.Add(this.label4);
      this.groupBox1.Controls.Add(this.keyTextBox);
      this.groupBox1.Controls.Add(this.label3);
      this.groupBox1.Controls.Add(this.label2);
      this.groupBox1.Controls.Add(this.descriptionTextBox);
      this.groupBox1.Controls.Add(this.label1);
      this.groupBox1.Controls.Add(this.keyTreeView);
      this.groupBox1.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
      this.groupBox1.Location = new System.Drawing.Point(0, 0);
      this.groupBox1.Name = "groupBox1";
      this.groupBox1.Size = new System.Drawing.Size(472, 408);
      this.groupBox1.TabIndex = 0;
      this.groupBox1.TabStop = false;
      this.groupBox1.Text = "Assignments";
      // 
      // idTextBox
      // 
      this.idTextBox.Anchor =
        ((System.Windows.Forms.AnchorStyles)
         (((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
           | System.Windows.Forms.AnchorStyles.Right)));
      this.idTextBox.BorderColor = System.Drawing.Color.Empty;
      this.idTextBox.Enabled = false;
      this.idTextBox.Location = new System.Drawing.Point(168, 324);
      this.idTextBox.MaxLength = 5;
      this.idTextBox.Name = "idTextBox";
      this.idTextBox.Size = new System.Drawing.Size(40, 20);
      this.idTextBox.TabIndex = 6;
      this.idTextBox.Visible = false;
      this.idTextBox.TextChanged += new System.EventHandler(this.idTextBox_TextChanged);
      this.idTextBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.idTextBox_KeyPress);
      // 
      // idComboBox
      // 
      this.idComboBox.Anchor =
        ((System.Windows.Forms.AnchorStyles)
         (((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
           | System.Windows.Forms.AnchorStyles.Right)));
      this.idComboBox.BorderColor = System.Drawing.Color.Empty;
      this.idComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this.idComboBox.Enabled = false;
      this.idComboBox.Location = new System.Drawing.Point(168, 324);
      this.idComboBox.Name = "idComboBox";
      this.idComboBox.Size = new System.Drawing.Size(288, 21);
      this.idComboBox.TabIndex = 7;
      this.idComboBox.SelectedIndexChanged += new System.EventHandler(this.idComboBox_SelectedIndexChanged);
      // 
      // addButton
      // 
      this.addButton.Anchor =
        ((System.Windows.Forms.AnchorStyles)
         ((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
      this.addButton.Enabled = false;
      this.addButton.Location = new System.Drawing.Point(304, 272);
      this.addButton.Name = "addButton";
      this.addButton.Size = new System.Drawing.Size(72, 22);
      this.addButton.TabIndex = 1;
      this.addButton.Text = "Add";
      this.addButton.UseVisualStyleBackColor = true;
      this.addButton.Click += new System.EventHandler(this.addButton_Click);
      // 
      // deleteButton
      // 
      this.deleteButton.Anchor =
        ((System.Windows.Forms.AnchorStyles)
         ((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
      this.deleteButton.Enabled = false;
      this.deleteButton.Location = new System.Drawing.Point(384, 272);
      this.deleteButton.Name = "deleteButton";
      this.deleteButton.Size = new System.Drawing.Size(72, 22);
      this.deleteButton.TabIndex = 2;
      this.deleteButton.Text = "Delete";
      this.deleteButton.UseVisualStyleBackColor = true;
      this.deleteButton.Click += new System.EventHandler(this.deleteButton_Click);
      // 
      // fileNameButton
      // 
      this.fileNameButton.Anchor =
        ((System.Windows.Forms.AnchorStyles)
         ((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
      this.fileNameButton.Enabled = false;
      this.fileNameButton.Location = new System.Drawing.Point(384, 371);
      this.fileNameButton.Name = "fileNameButton";
      this.fileNameButton.Size = new System.Drawing.Size(72, 22);
      this.fileNameButton.TabIndex = 12;
      this.fileNameButton.Text = "Browse";
      this.fileNameButton.UseVisualStyleBackColor = true;
      this.fileNameButton.Click += new System.EventHandler(this.fileNameButton_Click);
      // 
      // soundTextBox
      // 
      this.soundTextBox.Anchor =
        ((System.Windows.Forms.AnchorStyles)
         (((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
           | System.Windows.Forms.AnchorStyles.Right)));
      this.soundTextBox.BorderColor = System.Drawing.Color.Empty;
      this.soundTextBox.Enabled = false;
      this.soundTextBox.Location = new System.Drawing.Point(168, 372);
      this.soundTextBox.Name = "soundTextBox";
      this.soundTextBox.Size = new System.Drawing.Size(208, 20);
      this.soundTextBox.TabIndex = 11;
      this.soundTextBox.TextChanged += new System.EventHandler(this.soundTextBox_TextChanged);
      // 
      // label4
      // 
      this.label4.Anchor =
        ((System.Windows.Forms.AnchorStyles)
         ((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      this.label4.Location = new System.Drawing.Point(16, 376);
      this.label4.Name = "label4";
      this.label4.Size = new System.Drawing.Size(40, 16);
      this.label4.TabIndex = 10;
      this.label4.Text = "Sound:";
      // 
      // keyTextBox
      // 
      this.keyTextBox.AcceptsReturn = true;
      this.keyTextBox.Anchor =
        ((System.Windows.Forms.AnchorStyles)
         (((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
           | System.Windows.Forms.AnchorStyles.Right)));
      this.keyTextBox.BorderColor = System.Drawing.Color.Empty;
      this.keyTextBox.Enabled = false;
      this.keyTextBox.Location = new System.Drawing.Point(168, 348);
      this.keyTextBox.Name = "keyTextBox";
      this.keyTextBox.ReadOnly = true;
      this.keyTextBox.Size = new System.Drawing.Size(288, 20);
      this.keyTextBox.TabIndex = 9;
      this.keyTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.keyTextBox_KeyDown);
      this.keyTextBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.keyTextBox_KeyPress);
      // 
      // label3
      // 
      this.label3.Anchor =
        ((System.Windows.Forms.AnchorStyles)
         ((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      this.label3.Location = new System.Drawing.Point(16, 352);
      this.label3.Name = "label3";
      this.label3.Size = new System.Drawing.Size(32, 16);
      this.label3.TabIndex = 8;
      this.label3.Text = "Key:";
      // 
      // label2
      // 
      this.label2.Anchor =
        ((System.Windows.Forms.AnchorStyles)
         ((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      this.label2.Location = new System.Drawing.Point(16, 328);
      this.label2.Name = "label2";
      this.label2.Size = new System.Drawing.Size(40, 16);
      this.label2.TabIndex = 5;
      this.label2.Text = "Action:";
      // 
      // descriptionTextBox
      // 
      this.descriptionTextBox.Anchor =
        ((System.Windows.Forms.AnchorStyles)
         (((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
           | System.Windows.Forms.AnchorStyles.Right)));
      this.descriptionTextBox.BorderColor = System.Drawing.Color.Empty;
      this.descriptionTextBox.Enabled = false;
      this.descriptionTextBox.Location = new System.Drawing.Point(168, 300);
      this.descriptionTextBox.Name = "descriptionTextBox";
      this.descriptionTextBox.Size = new System.Drawing.Size(288, 20);
      this.descriptionTextBox.TabIndex = 4;
      this.descriptionTextBox.TextChanged += new System.EventHandler(this.descriptionTextBox_TextChanged);
      // 
      // label1
      // 
      this.label1.Anchor =
        ((System.Windows.Forms.AnchorStyles)
         ((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      this.label1.Location = new System.Drawing.Point(16, 304);
      this.label1.Name = "label1";
      this.label1.Size = new System.Drawing.Size(64, 16);
      this.label1.TabIndex = 3;
      this.label1.Text = "Description:";
      // 
      // keyTreeView
      // 
      this.keyTreeView.Anchor =
        ((System.Windows.Forms.AnchorStyles)
         ((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
           | System.Windows.Forms.AnchorStyles.Right)));
      this.keyTreeView.FullRowSelect = true;
      this.keyTreeView.HideSelection = false;
      this.keyTreeView.Location = new System.Drawing.Point(16, 24);
      this.keyTreeView.Name = "keyTreeView";
      this.keyTreeView.Size = new System.Drawing.Size(440, 240);
      this.keyTreeView.TabIndex = 0;
      this.keyTreeView.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.keyTreeView_AfterSelect);
      // 
      // Keys
      // 
      this.Controls.Add(this.groupBox1);
      this.Name = "Keys";
      this.Size = new System.Drawing.Size(472, 408);
      this.groupBox1.ResumeLayout(false);
      this.groupBox1.PerformLayout();
      this.ResumeLayout(false);
    }

    #endregion

    private void keyTreeView_AfterSelect(object sender, TreeViewEventArgs e)
    {
      currentlySelectedNode = e.Node;

      addButton.Enabled = currentlySelectedNode != null &&
                          (!(currentlySelectedNode.Tag is KeyAction) && currentlySelectedNode.Tag != null);

      if (addButton.Enabled == false)
      {
        addButton.Enabled = currentlySelectedNode == globalNode || currentlySelectedNode == windowsNode;
      }

      //
      // Enable/Disable controls
      //
      deleteButton.Enabled =
        keyTextBox.Enabled =
        fileNameButton.Enabled =
        idComboBox.Enabled =
        idTextBox.Enabled = soundTextBox.Enabled = descriptionTextBox.Enabled = e.Node.Tag is KeyAction;

      if (deleteButton.Enabled == false)
      {
        deleteButton.Enabled = currentlySelectedNode.Tag is ActionWindow;
      }

      if (e.Node.Tag is ActionWindow)
      {
        //
        // Enable correct controls
        //
        idTextBox.Visible = true;
        idComboBox.Visible = false;

        idTextBox.Enabled = true;

        ActionWindow window = e.Node.Tag as ActionWindow;

        descriptionTextBox.Text = window.Description;
        idTextBox.Text = window.Id.ToString();

        keyTextBox.Text = string.Empty;
        soundTextBox.Text = string.Empty;

        descriptionTextBox.Enabled = idComboBox.Enabled = true;
      }
      else if (e.Node.Tag is KeyAction)
      {
        //
        // Enable correct controls
        //
        idTextBox.Visible = false;
        idComboBox.Visible = true;

        KeyAction action = e.Node.Tag as KeyAction;

        descriptionTextBox.Text = action.Description;
        idComboBox.Text = GetActionName(action.Id);
        keyTextBox.Text = action.Key;
        soundTextBox.Text = action.Sound;
      }
      else
      {
        //
        // None of the above
        //	
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void fileNameButton_Click(object sender, EventArgs e)
    {
      using (openFileDialog = new OpenFileDialog())
      {
        openFileDialog.FileName = soundTextBox.Text;
        openFileDialog.CheckFileExists = true;
        openFileDialog.RestoreDirectory = true;
        openFileDialog.Filter = "wav files (*.wav)|*.wav";
        openFileDialog.FilterIndex = 0;
        openFileDialog.Title = "Select sound file";

        DialogResult dialogResult = openFileDialog.ShowDialog();

        if (dialogResult == DialogResult.OK)
        {
          soundTextBox.Text = Path.GetFileName(openFileDialog.FileName);
        }
      }
    }

    private void descriptionTextBox_TextChanged(object sender, EventArgs e)
    {
      if (currentlySelectedNode != null)
      {
        int id = 0;

        //
        // Update the actual node
        //
        if (currentlySelectedNode.Tag is ActionWindow)
        {
          ActionWindow window = currentlySelectedNode.Tag as ActionWindow;
          window.Description = descriptionTextBox.Text;
          id = window.Id;
        }
        else if (currentlySelectedNode.Tag is KeyAction)
        {
          KeyAction action = currentlySelectedNode.Tag as KeyAction;
          action.Description = descriptionTextBox.Text;
          id = action.Id;
        }

        //
        // Update visible stuff
        //
        currentlySelectedNode.Text = String.Format("{0} ({1})", descriptionTextBox.Text, id);
      }
    }

    private void keyTextBox_KeyDown(object sender, KeyEventArgs e)
    {
      if (keyMappings.IsValid(e.KeyValue))
      {
        keyTextBox.Text = keyMappings.GetName(e.KeyValue);

        if (currentlySelectedNode != null)
        {
          if (currentlySelectedNode.Tag is KeyAction)
          {
            KeyAction action = currentlySelectedNode.Tag as KeyAction;

            action.Key = keyTextBox.Text;

            currentlySelectedNode.Nodes[1].Text = String.Format("Key = " + keyTextBox.Text);
          }
        }
      }
      else
      {
        e.SuppressKeyPress = true;
      }
    }

    private void keyTextBox_KeyPress(object sender, KeyPressEventArgs e)
    {
      keyTextBox.Text = e.KeyChar.ToString().ToUpper(); // keyMappings.GetName(e.KeyValue);

      if (currentlySelectedNode != null)
      {
        if (currentlySelectedNode.Tag is KeyAction)
        {
          KeyAction action = currentlySelectedNode.Tag as KeyAction;

          action.Key = keyTextBox.Text;

          currentlySelectedNode.Nodes[1].Text = String.Format("Key = " + keyTextBox.Text);
        }
      }
    }

    private void addButton_Click(object sender, EventArgs e)
    {
      if (currentlySelectedNode != null)
      {
        if (currentlySelectedNode == globalNode)
        {
          //
          // Add an action to the global node
          //
          KeyAction action = new KeyAction(0, "", "<New Action>", "");
          globalActions.Add(action);

          TreeNode actionNode = new TreeNode(action.Description + " (" + action.Id + ")");

          actionNode.Nodes.Add("Action = " + GetActionName(action.Id));
          actionNode.Nodes.Add("Key = " + action.Key);
          actionNode.Nodes.Add("Sound = " + action.Sound);

          actionNode.Tag = action;

          globalNode.Nodes.Add(actionNode);

          //
          // Make node visible and select it
          //
          actionNode.EnsureVisible();
          keyTreeView.SelectedNode = actionNode;
        }
        else if (currentlySelectedNode == windowsNode)
        {
          //
          // Add window
          //
          ActionWindow window = new ActionWindow();
          windowActions.Add(window);

          window.Id = 0;
          window.Description = "<New Window>";

          TreeNode windowNode = new TreeNode(window.Description + " (" + window.Id + ")");

          windowNode.Tag = window;

          windowsNode.Nodes.Add(windowNode);

          //
          // Make node visible and select it
          //
          windowNode.EnsureVisible();
          keyTreeView.SelectedNode = windowNode;
        }
        else if (currentlySelectedNode.Tag is ActionWindow)
        {
          ActionWindow window = currentlySelectedNode.Tag as ActionWindow;

          //
          // Add an action to the selected window
          //
          KeyAction action = new KeyAction(0, "", "<New Action>", "");
          window.Actions.Add(action);

          TreeNode actionNode = new TreeNode(action.Description + " (" + action.Id + ")");

          actionNode.Nodes.Add("Action = " + GetActionName(action.Id));
          actionNode.Nodes.Add("Key = " + action.Key);
          actionNode.Nodes.Add("Sound = " + action.Sound);

          actionNode.Tag = action;

          currentlySelectedNode.Nodes.Add(actionNode);

          //
          // Make node visible and select it
          //
          actionNode.EnsureVisible();
          keyTreeView.SelectedNode = actionNode;
        }
      }
    }

    private void deleteButton_Click(object sender, EventArgs e)
    {
      if (currentlySelectedNode != null)
      {
        //
        // Fetch parent node
        //
        TreeNode parentNode = currentlySelectedNode.Parent;

        if (parentNode == globalNode)
        {
          globalActions.Remove(currentlySelectedNode.Tag);
        }
        else if (parentNode == windowsNode)
        {
          windowActions.Remove(currentlySelectedNode.Tag);
        }
        else if (parentNode.Tag is ActionWindow)
        {
          ActionWindow window = parentNode.Tag as ActionWindow;

          window.Actions.Remove(currentlySelectedNode.Tag);
        }

        parentNode.Nodes.Remove(currentlySelectedNode);
      }
    }

    private void idTextBox_TextChanged(object sender, EventArgs e)
    {
      if (currentlySelectedNode != null)
      {
        //
        // Update the actual node
        //
        if (currentlySelectedNode.Tag is ActionWindow)
        {
          ActionWindow window = currentlySelectedNode.Tag as ActionWindow;

          window.Id = idTextBox.Text.Length > 0 ? Convert.ToInt32(idTextBox.Text) : 0;

          currentlySelectedNode.Text = window.Description + " (" + window.Id + ")";
        }
      }
    }

    private void idTextBox_KeyPress(object sender, KeyPressEventArgs e)
    {
      if (char.IsNumber(e.KeyChar) == false && e.KeyChar != 8)
      {
        e.Handled = true;
      }
    }

    private void soundTextBox_TextChanged(object sender, EventArgs e)
    {
      if (currentlySelectedNode != null)
      {
        if (currentlySelectedNode.Tag is KeyAction)
        {
          KeyAction action = currentlySelectedNode.Tag as KeyAction;

          action.Sound = soundTextBox.Text;

          currentlySelectedNode.Nodes[2].Text = String.Format("Sound = " + action.Sound);
        }
      }
    }

    private void idComboBox_SelectedIndexChanged(object sender, EventArgs e)
    {
      if (currentlySelectedNode != null)
      {
        //
        // Update the actual node
        //
        if (currentlySelectedNode.Tag is ActionWindow)
        {
          ActionWindow window = currentlySelectedNode.Tag as ActionWindow;
          //          window.Id = Convert.ToInt32(idTextBox.Text);
          //
          //          currentlySelectedNode.Text = window.Description + " (" + window.Id + ")";
        }
        else if (currentlySelectedNode.Tag is KeyAction)
        {
          if (idComboBox.Text != null)
          {
            KeyAction action = currentlySelectedNode.Tag as KeyAction;

            try
            {
              Action.ActionType actionType = (Action.ActionType)Enum.Parse(typeof (Action.ActionType), idComboBox.Text);

              action.Id = (int)actionType;

              currentlySelectedNode.Text = action.Description + " (" + action.Id + ")";
              currentlySelectedNode.Nodes[0].Text = String.Format("Action = " + GetActionName(action.Id));
            }
            catch {}
          }
        }
      }
    }
  }

  public class KeyMappings : Hashtable
  {
    public class KeyMap
    {
      public int Code;
      public System.Windows.Forms.Keys Key;
      public string Name;
      public string SettingName;
      public bool Valid = true;

      public KeyMap(int code, System.Windows.Forms.Keys key, string name, string settingName)
      {
        this.Code = code;
        this.Key = key;
        this.SettingName = settingName;
        this.Name = name;
      }

      public KeyMap(int code, System.Windows.Forms.Keys key, string name, string settingName, bool valid)
      {
        this.Code = code;
        this.Key = key;
        this.SettingName = settingName;
        this.Name = name;
        this.Valid = valid;
      }
    }

    public KeyMappings()
    {
      //
      // Add valid keys, keys not added are automatically valid
      //
      Add(System.Windows.Forms.Keys.F1, "F1");
      Add(System.Windows.Forms.Keys.F2, "F2");
      Add(System.Windows.Forms.Keys.F3, "F3");
      Add(System.Windows.Forms.Keys.F4, "F4");
      Add(System.Windows.Forms.Keys.F5, "F5");
      Add(System.Windows.Forms.Keys.F6, "F6");
      Add(System.Windows.Forms.Keys.F7, "F7");
      Add(System.Windows.Forms.Keys.F8, "F8");
      Add(System.Windows.Forms.Keys.F9, "F9");
      Add(System.Windows.Forms.Keys.F10, "F10");
      Add(System.Windows.Forms.Keys.F11, "F11");
      Add(System.Windows.Forms.Keys.F12, "F12");

      Add(System.Windows.Forms.Keys.Back, "Backspace");
      Add(System.Windows.Forms.Keys.Tab, "Tab");
      Add(System.Windows.Forms.Keys.End, "End");
      Add(System.Windows.Forms.Keys.Insert, "Insert");
      Add(System.Windows.Forms.Keys.Home, "Home");
      Add(System.Windows.Forms.Keys.PageUp, "PageUp");
      Add(System.Windows.Forms.Keys.PageDown, "PageDown");
      Add(System.Windows.Forms.Keys.Left, "Left");
      Add(System.Windows.Forms.Keys.Right, "Right");
      Add(System.Windows.Forms.Keys.Down, "Down");
      Add(System.Windows.Forms.Keys.Up, "Up");
      Add(System.Windows.Forms.Keys.Enter, "Enter");
      Add(System.Windows.Forms.Keys.Delete, "Delete");
      Add(System.Windows.Forms.Keys.Pause, "Pause");
      Add(System.Windows.Forms.Keys.PrintScreen, "PrintScreen");
      Add(System.Windows.Forms.Keys.Escape, "Escape");
      Add(System.Windows.Forms.Keys.Space, "Space");

      //
      // Following keys are not valid
      //
      Add(System.Windows.Forms.Keys.ShiftKey, "Shift", false);
      Add(System.Windows.Forms.Keys.CapsLock, "CapsLock", false);
      Add(System.Windows.Forms.Keys.Alt, "Alt", false);
      Add(System.Windows.Forms.Keys.Menu, "AltKey", false);
      Add(System.Windows.Forms.Keys.NumLock, "NumLock", false);
      Add(System.Windows.Forms.Keys.ControlKey, "Control", false);
    }

    public bool IsValid(int code)
    {
      bool result = true;

      if (ContainsKey(code))
      {
        KeyMap key = this[code] as KeyMap;
        result = key.Valid;
      }
      return result;
    }

    public void Add(int code, System.Windows.Forms.Keys key, string name, string settingName, bool valid)
    {
      Add(code, new KeyMap(code, key, name, settingName, valid));
    }

    public void Add(System.Windows.Forms.Keys key, string name, string settingName)
    {
      Add((int)key, key, name, settingName, true);
    }

    public void Add(System.Windows.Forms.Keys key, string name)
    {
      Add((int)key, key, name, name, true);
    }

    public void Add(System.Windows.Forms.Keys key, string name, bool valid)
    {
      Add((int)key, key, name, name, valid);
    }

    public string GetName(int code)
    {
      string result = string.Empty;

      if (ContainsKey(code))
      {
        KeyMap key = this[code] as KeyMap;
        result = key.Name;
      }
      else
      {
        //
        // Item wasn't found in map, this must be a common key
        //
        result = String.Format("{0}", (char)code);
      }

      return result;
    }

    public string GetSettingName(int code)
    {
      string result = string.Empty;

      if (ContainsKey(code))
      {
        KeyMap key = this[code] as KeyMap;
        result = key.SettingName;
      }
      else
      {
        //
        // Item wasn't found in map, this must be a common key
        //
        result = String.Format("{0}", (char)code);
      }

      return result;
    }
  }

  public class KeyAction
  {
    public string Description;
    public int Id;
    public string Key;
    public string Sound;

    public bool HasSound
    {
      get { return Sound.Length > 0; }
    }

    public KeyAction(int id, string key, string description, string sound)
    {
      this.Id = id;
      this.Key = key;
      this.Description = description;
      this.Sound = sound;
    }
  }

  public class ActionWindow
  {
    public string Description;
    public int Id;
    public ArrayList Actions = new ArrayList();
  }
}