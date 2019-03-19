using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Imaging;

namespace compression
{
    public partial class Form1 : Form
    {
        Form3 form3;
        Bitmap bit1, bit2;
        Bitmap intraFrame;
        public Form1()
        {
            InitializeComponent();
            form3 = new Form3(this);

        }

        //convert img1
        private void ConvertImg(object sender, EventArgs e)
        {
            intraFrame = this.form3.convertIntraFrame(bit1);
            pictureBox2.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox2.Image = intraFrame;

        }

        //open img1
        private void OpenImage1(object sender, EventArgs e)
        {
            OpenFileDialog openFile = new OpenFileDialog();
            openFile.Filter = "Image Files(*.jpg; *.jpeg; *.gif; *.bmp)|*.jpg; *.jpeg; *.gif; *.bmp";
            if (openFile.ShowDialog() == DialogResult.OK)
            {
                
                bit1 = new Bitmap(openFile.FileName);
                pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
                pictureBox1.Image = bit1;
            }
        }

        //open img2
        private void OpenImage2(object sender, EventArgs e)
        {
            OpenFileDialog openFile = new OpenFileDialog();
            openFile.Filter = "Image Files(*.jpg; *.jpeg; *.gif; *.bmp)|*.jpg; *.jpeg; *.gif; *.bmp";
            if (openFile.ShowDialog() == DialogResult.OK)
            {
                bit2 = new Bitmap(openFile.FileName);
                pictureBox3.SizeMode = PictureBoxSizeMode.StretchImage;
                pictureBox3.Image = bit2;
            }
        }


        private void Compression(object sender, EventArgs e)
        {
            Bitmap interFrame = this.form3.convertInterFrame(bit2);
            pictureBox4.SizeMode = PictureBoxSizeMode.StretchImage;

            pictureBox4.Image = interFrame;

            
            status1.Text = "origin size: " + (interFrame.Width* interFrame.Height* 3*2) + "bytes";

            int size = this.form3.fileSize();
            status2.Text = "compress size: " + size + "bytes";
        }

        //save file
        private void SaveFile(object sender, EventArgs e)
        {
            SaveFileDialog file = new SaveFileDialog();
            file.Filter = "Image(*.vid)|*.vid";
            if(file.ShowDialog() == DialogResult.OK)
            {
                form3.save(file.OpenFile());
            }
        }


        //load file
        private void LoadFile(object sender, EventArgs e)
        {
            OpenFileDialog openFile = new OpenFileDialog();
            openFile.Filter = "Image(*.vid)|*.vid";

            if (openFile.ShowDialog() == DialogResult.OK)
            {
                Bitmap[] ret = form3.load(openFile.OpenFile());
                pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
                pictureBox1.Image = ret[0];
                pictureBox3.SizeMode = PictureBoxSizeMode.StretchImage;
                pictureBox3.Image = ret[1];
            }


        }

        private void clearToolStripMenuItem_Click(object sender, EventArgs e)
        {
            pictureBox1.Image = null;
            pictureBox2.Image = null;
            pictureBox3.Image = null;
            pictureBox4.Image = null;

            status1.Text = "origin size: ";
            status2.Text = "compress size: ";
        }

        

        
    }
}
