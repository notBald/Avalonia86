﻿using Microsoft.Win32;
using System;
using System.IO;
using System.Windows.Forms;

namespace _86boxManager
{
    public partial class dlgSettings : Form
    {
        private bool settingsChanged = false; //Keeps track of unsaved changes

        public dlgSettings()
        {
            InitializeComponent();
        }

        private void dlgSettings_Load(object sender, EventArgs e)
        {
            LoadSettings();
        }

        private void dlgSettings_FormClosing(object sender, FormClosingEventArgs e)
        {
            //Unsaved changes, ask the user to confirm
            if (settingsChanged == true)
            {
                e.Cancel = true;
                DialogResult result = MessageBox.Show("Would you like to save the changes you've made to the settings?", "Unsaved changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    SaveSettings();
                }
                if (result != DialogResult.Cancel)
                {
                    e.Cancel = false;
                }
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            SaveSettings();
            settingsChanged = CheckForChanges();
            btnApply.Enabled = settingsChanged;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (settingsChanged)
            {
                SaveSettings();
            }
            Close();
        }

        private void txt_TextChanged(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtEXEdir.Text) || string.IsNullOrWhiteSpace(txtCFGdir.Text) ||
                string.IsNullOrWhiteSpace(txtLaunchTimeout.Text))
            {
                btnApply.Enabled = false;
            }
            else
            {
                settingsChanged = CheckForChanges();
                btnApply.Enabled = settingsChanged;
            }
        }

        //TODO: Rewrite
        //Save the settings to the registry
        private void SaveSettings()
        {
            if (!File.Exists(txtEXEdir.Text + "86Box.exe") && !File.Exists(txtEXEdir.Text + @"\86Box.exe"))
            {
                MessageBox.Show("86Box.exe could not be found in the directory you specified. Make sure the path is correct or you won't be able to use any virtual machines.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            try
            {
                RegistryKey regkey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\86Box", true); //Try to open the key first (in read-write mode) to see if it already exists
                if (regkey == null) //Regkey doesn't exist yet, must be created first and then reopened
                {
                    Registry.CurrentUser.CreateSubKey(@"SOFTWARE\86Box");
                    regkey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\86Box", true);
                    regkey.CreateSubKey("Virtual Machines");
                }

                //Store the new values, close the key, changes are saved
                regkey.SetValue("EXEdir", txtEXEdir.Text, RegistryValueKind.String);
                regkey.SetValue("CFGdir", txtCFGdir.Text, RegistryValueKind.String);
                regkey.SetValue("MinimizeOnVMStart", cbxMinimize.Checked, RegistryValueKind.DWord);
                regkey.SetValue("ShowConsole", cbxShowConsole.Checked, RegistryValueKind.DWord);
                regkey.SetValue("MinimizeToTray", cbxMinimizeTray.Checked, RegistryValueKind.DWord);
                regkey.SetValue("CloseToTray", cbxCloseTray.Checked, RegistryValueKind.DWord);
                regkey.SetValue("LaunchTimeout", int.Parse(txtLaunchTimeout.Text), RegistryValueKind.DWord);
                regkey.Close();

                settingsChanged = CheckForChanges();

                MessageBox.Show("Settings were successfully saved.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("An error has occurred. Please provide the following information to the developer:\n" + ex.Message + "\n" + ex.StackTrace, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //TODO: Rewrite
        //Read the settings from the registry
        private void LoadSettings()
        {
            try
            {
                RegistryKey regkey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\86Box", false); //Open the key as read only

                //If the key doesn't exist yet, fallback to defaults
                if (regkey == null)
                {
                    MessageBox.Show("86Box Manager settings could not be loaded. This is normal if you're running 86Box Manager for the first time. Default values will be used.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                    //Create the key and reopen it for write access
                    Registry.CurrentUser.CreateSubKey(@"SOFTWARE\86Box");
                    regkey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\86Box", true);
                    regkey.CreateSubKey("Virtual Machines");

                    txtCFGdir.Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\86Box VMs\";
                    txtEXEdir.Text = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\86Box\";
                    cbxMinimize.Checked = false;
                    cbxShowConsole.Checked = true;
                    cbxMinimizeTray.Checked = false;
                    cbxCloseTray.Checked = false;
                    txtLaunchTimeout.Text = "5000";

                    SaveSettings(); //This will write the default values to the registry
                }
                else
                {
                    txtEXEdir.Text = regkey.GetValue("EXEdir").ToString();
                    txtCFGdir.Text = regkey.GetValue("CFGdir").ToString();
                    cbxMinimize.Checked = Convert.ToBoolean(regkey.GetValue("MinimizeOnVMStart"));
                    cbxShowConsole.Checked = Convert.ToBoolean(regkey.GetValue("ShowConsole"));
                    cbxMinimizeTray.Checked = Convert.ToBoolean(regkey.GetValue("MinimizeToTray"));
                    cbxCloseTray.Checked = Convert.ToBoolean(regkey.GetValue("CloseToTray"));
                    txtLaunchTimeout.Text = Convert.ToString(regkey.GetValue("LaunchTimeout"));
                }

                regkey.Close();
            }
            catch (Exception ex)
            {
                txtCFGdir.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\86Box VMs";
                txtEXEdir.Text = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\86Box";
                cbxMinimize.Checked = false;
                cbxShowConsole.Checked = true;
                cbxMinimizeTray.Checked = false;
                cbxCloseTray.Checked = false;
                txtLaunchTimeout.Text = "5000";
            }
        }

        private void btnBrowse1_Click(object sender, EventArgs e)
        {
            FolderSelectDialog dialog = new FolderSelectDialog
            {
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer),
                Title = "Select a folder where 86Box program files and the roms folder are located"
            };

            if (dialog.Show(Handle))
            {
                txtEXEdir.Text  = dialog.FileName;
                if (!txtEXEdir.Text.EndsWith(@"\")) //Just in case
                {
                    txtEXEdir.Text += @"\";
                }
            }
        }

        private void btnBrowse2_Click(object sender, EventArgs e)
        {
            FolderSelectDialog dialog = new FolderSelectDialog
            {
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer),
                Title = "Select a folder where your virtual machines (configs, nvr folders, etc.) will be located"
            };

            if (dialog.Show(Handle))
            {
                txtCFGdir.Text = dialog.FileName;
                if (!txtCFGdir.Text.EndsWith(@"\")) //Just in case
                {
                    txtCFGdir.Text += @"\";
                }
            }
        }

        private void cbxMinimize_CheckedChanged(object sender, EventArgs e)
        {
            settingsChanged = CheckForChanges();
        }

        private void cbxShowConsole_CheckedChanged(object sender, EventArgs e)
        {
            settingsChanged = CheckForChanges();
        }

        private void btnDefaults_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show("All settings will be reset to their default values. Do you wish to continue?", "Settings will be reset", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result == DialogResult.Yes)
            {
                ResetSettings();
            }
        }

        //Resets the settings to their default values
        private void ResetSettings()
        {
            RegistryKey regkey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\86Box", true);

            if (regkey == null)
            {
                Registry.CurrentUser.CreateSubKey(@"SOFTWARE\86Box");
                regkey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\86Box", true);
                regkey.CreateSubKey("Virtual Machines");
            }

            txtCFGdir.Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\86Box VMs\";
            txtEXEdir.Text = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + @"\86Box\";
            cbxMinimize.Checked = false;
            cbxShowConsole.Checked = true;
            cbxMinimizeTray.Checked = false;
            cbxCloseTray.Checked = false;
            txtLaunchTimeout.Text = "5000";

            SaveSettings();
            regkey.Close();
        }

        private void cbxCloseTray_CheckedChanged(object sender, EventArgs e)
        {
            settingsChanged = CheckForChanges();
        }

        private void cbxMinimizeTray_CheckedChanged(object sender, EventArgs e)
        {
            settingsChanged = CheckForChanges();
        }

        //Checks if all controls match the currently saved settings to determine if any changes were made
        private bool CheckForChanges()
        {
            RegistryKey regkey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\86Box");

            try
            {
                btnApply.Enabled = (txtEXEdir.Text != regkey.GetValue("EXEdir").ToString() ||
                    txtCFGdir.Text != regkey.GetValue("CFGdir").ToString() ||
                cbxMinimize.Checked != Convert.ToBoolean(regkey.GetValue("MinimizeOnVMStart")) ||
                cbxShowConsole.Checked != Convert.ToBoolean(regkey.GetValue("ShowConsole")) ||
                cbxMinimizeTray.Checked != Convert.ToBoolean(regkey.GetValue("MinimizeToTray")) ||
                cbxCloseTray.Checked != Convert.ToBoolean(regkey.GetValue("CloseToTray")) || 
                txtLaunchTimeout.Text != Convert.ToString(regkey.GetValue("LaunchTimeout")));

                return btnApply.Enabled;
            }
            catch (Exception ex)
            {
                return true; //For now let's just return true if anything goes wrong
            }
            finally
            {
                regkey.Close();
            }
        }
    }
}
