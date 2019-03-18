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

namespace compression
{
    class Form3
    {
        Form1 form1;
        Bitmap intraFrame, interFrame;
        int width;
        int height;

        //intra-frame
        byte[,] Y, Cb, Cr;
        double[,] YDCT, CbDCT, CrDCT;
        sbyte[,] YQuant, CbQuant, CrQuant;
        List<byte> YEncode, CbEncode, CrEncode;

        //inter-frame
        byte[,] YInter, CbInter, CrInter;
        Point[,] YMoVec;
        Point[,] CbMoVec;
        Point[,] CrMoVec;
        short[,] YDif, CbDif, CrDif;
        double[,] YDCTInter, CbDCTInter, CrDCTInter;
        sbyte[,] YQuantInter, CbQuantInter, CrQuantInter;
        List<byte> YEncodeInter, CbEncodeInter, CrEncodeInter;

        static readonly int[,] patternList = {
            {0,0},
            {1,0}, {0,1},
            {0,2}, {1,1}, {2,0},
            {3,0}, {2,1}, {1,2}, {0,3},
            {0,4}, {1,3}, {2,2}, {3,1}, {4,0},
            {5,0}, {4,1}, {3,2}, {2,3}, {1,4}, {0,5},
            {0,6}, {1,5}, {2,4}, {3,3}, {4,2}, {5,1}, {6,0},
            {7,0}, {6,1}, {5,2}, {4,3}, {3,4}, {2,5}, {1,6}, {0,7},
            {1,7}, {2,6}, {3,5}, {4,4}, {5,3}, {6,2}, {7,1},
            {7,2}, {6,3}, {5,4}, {4,5}, {3,6}, {2,7},
            {3,7}, {4,6}, {5,5}, {6,4}, {7,3},
            {7,4}, {6,5}, {5,6}, {4,7},
            {5,7}, {6,6}, {7,5},
            {7,6}, {6,7},
            {7,7}

        };


        private const int BLOCK_WIDTH = 8, BLOCK_HEIGHT = 8;
        private const int SQRT_MN = 8;
        private int[,] quantiza_matrix = new int[8, 8] {
            {16, 11, 10, 16, 24, 40, 51, 60},
            {12, 12, 14, 19, 26, 58, 60, 55},
            {14, 13, 16, 24, 40, 57, 69, 56},
            {14, 17, 22, 29, 51, 87, 80, 62},
            {18, 22, 37, 56, 68, 109, 103, 77},
            {24, 35, 55, 64, 81, 104, 113, 92 },
            {49, 64, 78, 87, 103, 121, 120, 101},
            {72, 92, 95, 98, 112, 100, 103, 99}
        };

        private int N_PW_2 = (int)Math.Pow(16, 2);
        private const int p = 15;

        const char id = '0';

        //constructor
        public Form3(Form1 form)
        {
            form1 = form;
        }

        //convert RGB to YCbCr forward and backward
        private void RGBtoYCbCr(Bitmap bit, byte[,] Y, byte[,] Cb, byte[,] Cr)
        {

            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    Color color = bit.GetPixel(i, j);
                    int red = color.R;
                    int green = color.G;
                    int blue = color.B;
                    Y[i, j] = (byte)Math.Max(Math.Min(((0.257 * red) + (0.504 * green) + (0.098 * blue) + 16), 255.0), 0);
                    Cb[i, j] = (byte)Math.Max(Math.Min(((-0.148 * red) + (-0.291 * green) + (0.439 * blue) + 128), 255.0), 0);
                    Cr[i, j] = (byte)Math.Max(Math.Min(((0.439 * red) + (-0.368 * green) + (-0.071 * blue) + 128), 255.0), 0);
                }
            }
        }
        private void YCbCrtoRGB(Bitmap bitmap, byte[,] Y, byte[,] Cb, byte[,] Cr)
        {
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    int red = (int)Math.Max(Math.Min((1.164 * (Y[i, j] - 16) + 1.596 * (Cr[i, j] - 128)), 255.0), 0);
                    int green = (int)Math.Max(Math.Min((1.164 * (Y[i, j] - 16) - 0.813 * (Cr[i, j] - 128) - 0.391 * (Cb[i, j] - 128)), 255.0), 0);
                    int blue = (int)Math.Max(Math.Min((1.164 * (Y[i, j] - 16) + 2.018 * (Cb[i, j] - 128)), 255.0), 0);
                    Color color = Color.FromArgb(red, green, blue);
                    bitmap.SetPixel(i, j, color);
                }
            }
        }

        //subsample and unsubsample
        private void subSampling(ref byte[,] Cb, ref byte[,] Cr)
        {
            byte[,] newCb = new byte[(width + 1) / 2, (height + 1) / 2];
            byte[,] newCr = new byte[(width + 1) / 2, (height + 1) / 2];
            for (int i = 0; i < height; i += 2)
            {
                for (int j = 0; j < width; j += 2)
                {
                    newCb[j / 2, i / 2] = Cb[j, i];
                    newCr[j / 2, i / 2] = Cr[j, i];
                }
            }
            Cb = newCb;
            Cr = newCr;
        }
        private void unsubSampling(ref byte[,] Cb, ref byte[,] Cr)
        {
            byte[,] newCb = new byte[width, height];
            byte[,] newCr = new byte[width, height];
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    newCb[j, i] = Cb[j / 2, i / 2];
                    newCr[j, i] = Cr[j / 2, i / 2];
                }
            }
            Cb = newCb;
            Cr = newCr;
        }

        //DCT and invDCT
        private double C(int num)
        {
            return num == 0 ? 1 / Math.Sqrt(2) : 1;
        }
        private double[,] DCT(byte[,] arr)
        {
            double[,] tmp = new double[arr.GetLength(0), arr.GetLength(1)];

            for (int h = 0; h < arr.GetLength(1); h += BLOCK_HEIGHT)
            {
                for (int w = 0; w < arr.GetLength(0); w += BLOCK_WIDTH)
                {
                    for (int u = 0; u < BLOCK_WIDTH; u++)
                    {
                        double c_u = C(u);

                        for (int v = 0; v < BLOCK_HEIGHT; v++)
                        {
                            double c_v = C(v);

                            double sum = 0;

                            for (int x = 0; x < BLOCK_WIDTH; x++)
                            {
                                for (int y = 0; y < BLOCK_HEIGHT; y++)
                                {
                                    sum += arr[x + w, y + h] * Math.Cos((2 * x + 1) * u * Math.PI / (2 * BLOCK_WIDTH)) * Math.Cos((2 * y + 1) * v * Math.PI / (2 * BLOCK_HEIGHT));
                                }
                            }

                            tmp[u + w, v + h] = ((2 * c_u * c_v / SQRT_MN) * sum);
                        }
                    }
                }
            }
            return tmp;
        }
        private byte[,] inverseDCT(double[,] arr)
        {
            byte[,] tmp = new byte[arr.GetLength(0), arr.GetLength(1)];
            for (int h = 0; h < arr.GetLength(1); h += BLOCK_HEIGHT)
            {
                for (int w = 0; w < arr.GetLength(0); w += BLOCK_WIDTH)
                {

                    for (int i = 0; i < BLOCK_WIDTH; i++)
                    {
                        for (int j = 0; j < BLOCK_HEIGHT; j++)
                        {
                            double sum = 0;

                            for (int u = 0; u < BLOCK_WIDTH; u++)
                            {
                                double c_u = C(u);

                                for (int v = 0; v < BLOCK_HEIGHT; v++)
                                {
                                    double c_v = C(v);
                                    sum += ((2 * c_u * c_v) / SQRT_MN) * Math.Cos((2 * i + 1) * u * Math.PI / (2 * BLOCK_WIDTH)) * Math.Cos((2 * j + 1) * v * Math.PI / (2 * BLOCK_HEIGHT)) * arr[u + w, v + h];
                                }
                            }
                            tmp[i + w, j + h] = (byte)sum;
                        }
                    }
                }
            }
            return tmp;
        }


        private double[,] DCT(short[,] arr)
        {
            double[,] tmp = new double[arr.GetLength(0), arr.GetLength(1)];

            for (int h = 0; h < arr.GetLength(1); h += BLOCK_HEIGHT)
            {
                for (int w = 0; w < arr.GetLength(0); w += BLOCK_WIDTH)
                {
                    for (int u = 0; u < BLOCK_WIDTH; u++)
                    {
                        double c_u = C(u);

                        for (int v = 0; v < BLOCK_HEIGHT; v++)
                        {
                            double c_v = C(v);

                            double sum = 0;

                            for (int x = 0; x < BLOCK_WIDTH; x++)
                            {
                                for (int y = 0; y < BLOCK_HEIGHT; y++)
                                {
                                    sum += arr[x + w, y + h] * Math.Cos((2 * x + 1) * u * Math.PI / (2 * BLOCK_WIDTH)) * Math.Cos((2 * y + 1) * v * Math.PI / (2 * BLOCK_HEIGHT));
                                }
                            }

                            tmp[u + w, v + h] = ((2 * c_u * c_v / SQRT_MN) * sum);
                        }
                    }
                }
            }
            return tmp;
        }
        private short[,] inverseDCTInter(double[,] arr)
        {
            short[,] tmp = new short[arr.GetLength(0), arr.GetLength(1)];
            for (int h = 0; h < arr.GetLength(1); h += BLOCK_HEIGHT)
            {
                for (int w = 0; w < arr.GetLength(0); w += BLOCK_WIDTH)
                {

                    for (int i = 0; i < BLOCK_WIDTH; i++)
                    {
                        for (int j = 0; j < BLOCK_HEIGHT; j++)
                        {
                            double sum = 0;

                            for (int u = 0; u < BLOCK_WIDTH; u++)
                            {
                                double c_u = C(u);

                                for (int v = 0; v < BLOCK_HEIGHT; v++)
                                {
                                    double c_v = C(v);
                                    sum += ((2 * c_u * c_v) / SQRT_MN) * Math.Cos((2 * i + 1) * u * Math.PI / (2 * BLOCK_WIDTH)) * Math.Cos((2 * j + 1) * v * Math.PI / (2 * BLOCK_HEIGHT)) * arr[u + w, v + h];
                                }
                            }
                            tmp[i + w, j + h] = (short)sum;
                        }
                    }
                }
            }
            return tmp;
        }

        //quantize and unquantize
        private sbyte[,] quantization(double[,] arr)
        {
            sbyte[,] tmp = new sbyte[arr.GetLength(0), arr.GetLength(1)];


            for (int i = 0; i < arr.GetLength(1); i += BLOCK_HEIGHT)
            {
                for (int j = 0; j < arr.GetLength(0); j += BLOCK_WIDTH)
                {
                    for (int w = 0; w < BLOCK_WIDTH; w++)
                    {
                        for (int h = 0; h < BLOCK_HEIGHT; h++)
                        {
                            tmp[i + w, j + h] = (sbyte)Math.Max(Math.Min(Math.Round(arr[i + w, j + h] / quantiza_matrix[w, h]), sbyte.MaxValue), sbyte.MinValue);

                        }
                    }
                }
            }
            return tmp;
        }
        private double[,] invQuantization(sbyte[,] arr)
        {
            double[,] tmp = new double[arr.GetLength(0), arr.GetLength(1)];
            for (int i = 0; i < arr.GetLength(1); i += BLOCK_HEIGHT)
            {
                for (int j = 0; j < arr.GetLength(0); j += BLOCK_WIDTH)
                {

                    for (int w = 0; w < BLOCK_WIDTH; w++)
                    {
                        for (int h = 0; h < BLOCK_HEIGHT; h++)
                        {
                            tmp[i + w, j + h] = arr[i + w, j + h] * quantiza_matrix[w, h];

                        }
                    }

                }
            }
            return tmp;
        }


        //encode and decode
        private List<byte> encoding(sbyte[,] arr) {
            List<byte> tmp = new List<byte>();

            for (int i = 0; i < arr.GetLength(1); i += BLOCK_HEIGHT)
            {
                for (int j = 0; j < arr.GetLength(0); j += BLOCK_WIDTH)
                {
                    sbyte prev = arr[j + patternList[0, 0], i + patternList[0, 1]];
                    byte count = 1;
                    for (int k = 1; k < patternList.GetLength(0); ++k)
                    {
                        if (prev == arr[j + patternList[k, 0], i + patternList[k, 1]])
                        {
                            ++count;
                        }
                        else if (count >= 3 || prev == 0)
                        {
                            tmp.Add((byte)0);
                            tmp.Add(count);
                            tmp.Add((byte)prev);
                            count = 1;
                        }
                        else
                        {
                            for (int l = 0; l < count; ++l)
                            {
                                tmp.Add((byte)prev);
                            }
                            count = 1;
                        }
                        prev = arr[j + patternList[k, 0], i + patternList[k, 1]];
                    }

                    if (count >= 3 || prev == 0)
                    {
                        tmp.Add((byte)0);
                        tmp.Add(count);
                        tmp.Add((byte)prev);
                        count = 1;
                    }
                    else
                    {
                        for (int l = 0; l < count; ++l)
                        {
                            tmp.Add((byte)prev);
                        }
                        count = 1;
                    }
                }
            }

            return tmp;
        }
        private sbyte[,] decoding(List<byte> list, int width, int height)
        {
            sbyte[,] tmp = new sbyte[width, height];
            int currentIndex = 0;
            for (int i = 0; i < tmp.GetLength(1); i += BLOCK_HEIGHT)
            {
                for (int j = 0; j < tmp.GetLength(0); j += BLOCK_WIDTH)
                {
                    for (int k = 0; k < patternList.GetLength(0);)
                    {
                        if (list.ElementAt(currentIndex) == 0)
                        {
                            currentIndex++;
                            byte count = list.ElementAt(currentIndex);
                            currentIndex++;
                            sbyte value = (sbyte)list.ElementAt(currentIndex);

                            for (int m = 0; m < count; m++)
                            {
                                tmp[patternList[k, 0] + j, i + patternList[k, 1]] = value;
                                k++;
                            }
                        }
                        else
                        {
                            sbyte value = (sbyte)list.ElementAt(currentIndex);
                            tmp[patternList[k, 0] + j, i + patternList[k, 1]] = value;
                            k++;
                        }
                        currentIndex++;
                    }


                }
            }

            return tmp;
        }

        //compression
        public Bitmap convertIntraFrame(Bitmap bit)
        {



            intraFrame = new Bitmap(bit.Width, bit.Height);
            width = intraFrame.Width;
            height = intraFrame.Height;
            Y = new byte[width, height];
            Cb = new byte[width, height];
            Cr = new byte[width, height];

            RGBtoYCbCr(bit, Y, Cb, Cr);
            subSampling(ref Cb, ref Cr);

            YDCT = new double[width, height];
            CbDCT = new double[Cb.GetLength(0), Cb.GetLength(1)];
            CrDCT = new double[Cr.GetLength(0), Cr.GetLength(1)];

            YQuant = new sbyte[width, height];
            CbQuant = new sbyte[Cb.GetLength(0), Cb.GetLength(1)];
            CrQuant = new sbyte[Cr.GetLength(0), Cr.GetLength(1)];

            YEncode = new List<byte>();
            CbEncode = new List<byte>();
            CrEncode = new List<byte>();

            YDCT = DCT(Y);
            CbDCT = DCT(Cb);
            CrDCT = DCT(Cr);

            YQuant = quantization(YDCT);
            CbQuant = quantization(CbDCT);
            CrQuant = quantization(CrDCT);

            YEncode = encoding(YQuant);
            CbEncode = encoding(CbQuant);
            CrEncode = encoding(CrQuant);

            YQuant = decoding(YEncode, YQuant.GetLength(0), YQuant.GetLength(1));
            CbQuant = decoding(CbEncode, CbQuant.GetLength(0), CbQuant.GetLength(1));
            CrQuant = decoding(CrEncode, CrQuant.GetLength(0), CrQuant.GetLength(1));

            YDCT = invQuantization(YQuant);
            CbDCT = invQuantization(CbQuant);
            CrDCT = invQuantization(CrQuant);

            Y = inverseDCT(YDCT);
            Cb = inverseDCT(CbDCT);
            Cr = inverseDCT(CrDCT);


            unsubSampling(ref Cb, ref Cr);
            YCbCrtoRGB(intraFrame, Y, Cb, Cr);

            return intraFrame;
        }


        public Bitmap convertInterFrame(Bitmap bit)
        {
            interFrame = new Bitmap(bit.Width, bit.Height);
            width = intraFrame.Width;
            height = intraFrame.Height;
            YInter = new byte[width, height];
            CbInter = new byte[width, height];
            CrInter = new byte[width, height];
            
            
            RGBtoYCbCr(bit, YInter, CbInter, CrInter);
            subSampling(ref CbInter, ref CrInter);
            subSampling(ref Cb, ref Cr);

            YDif = calcDifference(Y, YInter, out YMoVec);
            CbDif = calcDifference(Cb, CbInter, out CbMoVec);
            CrDif = calcDifference(Cr, CrInter, out CrMoVec);

            YDCTInter = new double[width, height];
            CbDCTInter = new double[CbInter.GetLength(0), CbInter.GetLength(1)];
            CrDCTInter = new double[CrInter.GetLength(0), CrInter.GetLength(1)];

            YQuantInter = new sbyte[width, height];
            CbQuantInter = new sbyte[CbInter.GetLength(0), CbInter.GetLength(1)];
            CrQuantInter = new sbyte[CrInter.GetLength(0), CrInter.GetLength(1)];

            YEncodeInter = new List<byte>();
            CbEncodeInter = new List<byte>();
            CrEncodeInter = new List<byte>();

            YDCTInter = DCT(YDif);
            CbDCTInter = DCT(CbDif);
            CrDCTInter = DCT(CrDif);

            YQuantInter = quantization(YDCTInter);
            CbQuantInter = quantization(CbDCTInter);
            CrQuantInter = quantization(CrDCTInter);

            YEncodeInter = encoding(YQuantInter);
            CbEncodeInter = encoding(CbQuantInter);
            CrEncodeInter = encoding(CrQuantInter);

            YQuantInter = decoding(YEncodeInter, YQuantInter.GetLength(0), YQuantInter.GetLength(1));
            CbQuantInter = decoding(CbEncodeInter, CbQuantInter.GetLength(0), CbQuantInter.GetLength(1));
            CrQuantInter = decoding(CrEncodeInter, CrQuantInter.GetLength(0), CrQuantInter.GetLength(1));

            YDCTInter = invQuantization(YQuantInter);
            CbDCTInter = invQuantization(CbQuantInter);
            CrDCTInter = invQuantization(CrQuantInter);

            YDif = inverseDCTInter(YDCTInter);
            CbDif = inverseDCTInter(CbDCTInter);
            CrDif = inverseDCTInter(CrDCTInter);

            YInter = unCalcDifference(Y, YMoVec, YDif);
            CbInter = unCalcDifference(Cb, CbMoVec, CbDif);
            CrInter = unCalcDifference(Cr, CrMoVec, CrDif);



            unsubSampling(ref CbInter, ref CrInter);
            YCbCrtoRGB(interFrame, YInter, CbInter, CrInter);

            return interFrame;

        }




        private short[,] calcDifference(byte[,] orgArr, byte[,] desArr, out Point[,] motion_vec)
        {
            short[,] dif = new short[orgArr.GetLength(0), orgArr.GetLength(1)];
            motion_vec = new Point[orgArr.GetLength(0) / BLOCK_WIDTH, orgArr.GetLength(1) / BLOCK_HEIGHT];
            for (int h = 0; h < orgArr.GetLength(1); h += BLOCK_HEIGHT)
            {
                for (int w = 0; w < orgArr.GetLength(0); w += BLOCK_WIDTH)
                {
                    short min = short.MaxValue;
                    Point vec = new Point();
                    for (int j = -p; j <= p; ++j)
                    {
                        for (int i = -p; i <=p; ++i)
                        {
                            int total = 0;
                            for (int x = 0; x < BLOCK_WIDTH; x++)
                            {
                                for (int y = 0; y < BLOCK_HEIGHT; y++)
                                {
                                    byte tmp = 0;
                                    if(!((i+w + x) < 0 || (i + w + x) >= orgArr.GetLength(0) ||(j + h + y) < 0 || (j + h + y) >= orgArr.GetLength(1)))
                                    {
                                        tmp = orgArr[i + w + x, j + h + y];
                                    }
                                    total += Math.Abs(desArr[w + x, h + y] - tmp);
                                }
                            }

                            if(min > total)
                            {
                                min = (short)total;
                                vec.X = i;
                                vec.Y = j;
                            }
                        }
                    }

                    motion_vec[w / BLOCK_WIDTH, h / BLOCK_HEIGHT] = vec;

                    for(int i = 0; i < BLOCK_WIDTH; i++)
                    {
                        for(int j = 0; j < BLOCK_HEIGHT; j++)
                        {
                            byte tmp = 0;
                            if (!((i + w + vec.X) < 0 || (i + w + vec.X) >= orgArr.GetLength(0) || (j + h + vec.Y) < 0 || (j + h + vec.Y) >= orgArr.GetLength(1)))
                            {
                                tmp = orgArr[i + w + vec.X, j + h + vec.Y];
                            }
                            dif[i+w, j+h] = (short) (desArr[i + w, j + h] - tmp);
                        }
                    }
                }
            }
            return dif;
        }
        private byte[,] unCalcDifference(byte[,] orgArr, Point[,] motion_vec, short[,] dif)
        {
            byte[,] desArr = new byte[orgArr.GetLength(0), orgArr.GetLength(1)];
            for (int h = 0; h < orgArr.GetLength(1); h += BLOCK_HEIGHT)
            {
                for (int w = 0; w < orgArr.GetLength(0); w += BLOCK_WIDTH)
                {
                    for(int x =0; x < BLOCK_WIDTH; x++)
                    {
                        for(int y = 0; y < BLOCK_HEIGHT; y++)
                        {

                            byte tmp = 0;
                            int posX = x + w + motion_vec[w / BLOCK_WIDTH, h / BLOCK_HEIGHT].X;
                            int posY = y + h + motion_vec[w / BLOCK_WIDTH, h / BLOCK_HEIGHT].Y;
                            if (!(posX < 0 || posX >= orgArr.GetLength(0) || posY < 0 || posY >= orgArr.GetLength(1)))
                            {
                                tmp = orgArr[posX, posY];
                            }
                            desArr[w+x, h+y]=(byte) Math.Max(Math.Min((tmp + dif[w+x, h+y]), 255.0), 0);
                        }
                    }
                }
            }
            return desArr;
        }

        public Bitmap[] load(Stream stream)
        {
            BinaryReader binaryReader = new BinaryReader(stream);

            if (binaryReader.ReadChar() != id)
            {
                //
            }

            width = binaryReader.ReadInt32();
            height = binaryReader.ReadInt32();

            //intra frame
            int count = binaryReader.ReadInt32();
            YEncode = new List<byte>(binaryReader.ReadBytes(count));

            count = binaryReader.ReadInt32();
            CbEncode = new List<byte>(binaryReader.ReadBytes(count));

            count = binaryReader.ReadInt32();
            CrEncode = new List<byte>(binaryReader.ReadBytes(count));

            //inter frame
            count = binaryReader.ReadInt32();
            YEncodeInter = new List<byte>(binaryReader.ReadBytes(count));
            count = binaryReader.ReadInt32();
            CbEncodeInter = new List<byte>(binaryReader.ReadBytes(count));
            count = binaryReader.ReadInt32();
            CrEncodeInter = new List<byte>(binaryReader.ReadBytes(count));

            //motion vector
            int x = binaryReader.ReadInt32();
            int y = binaryReader.ReadInt32();
            YMoVec = new Point[x, y];
            for (int i =0; i < x; i++)
            {
                for(int j = 0; j < y; j++)
                {
                    YMoVec[i, j].X = binaryReader.ReadInt32();
                    YMoVec[i, j].Y = binaryReader.ReadInt32();
                }
            }

            x = binaryReader.ReadInt32();
            y = binaryReader.ReadInt32();
            CbMoVec = new Point[x, y];
            for (int i = 0; i < x; i++)
            {
                for (int j = 0; j < y; j++)
                {
                    CbMoVec[i, j].X = binaryReader.ReadInt32();
                    CbMoVec[i, j].Y = binaryReader.ReadInt32();
                }
            }

            x = binaryReader.ReadInt32();
            y = binaryReader.ReadInt32();
            CrMoVec = new Point[x, y];
            for (int i = 0; i < x; i++)
            {
                for (int j = 0; j < y; j++)
                {
                    CrMoVec[i, j].X = binaryReader.ReadInt32();
                    CrMoVec[i, j].Y = binaryReader.ReadInt32();
                }
            }


      
            Bitmap[] ret = new Bitmap[2];

            ret[0] = new Bitmap(width, height);
            ret[1] = new Bitmap(width, height);

            //decode intra frame
            YQuant = decoding(YEncode, width, height);
            CbQuant = decoding(CbEncode, width/2, height/2);
            CrQuant = decoding(CrEncode, width / 2, height / 2);

            YDCT = invQuantization(YQuant);
            CbDCT = invQuantization(CbQuant);
            CrDCT = invQuantization(CrQuant);

            Y = inverseDCT(YDCT);
            Cb = inverseDCT(CbDCT);
            Cr = inverseDCT(CrDCT);


            unsubSampling(ref Cb, ref Cr);
            YCbCrtoRGB(ret[0], Y, Cb, Cr);

            //decode inter frame
            subSampling(ref Cb, ref Cr);
            YQuantInter = decoding(YEncodeInter, width, height);
            CbQuantInter = decoding(CbEncodeInter, width / 2, height / 2);
            CrQuantInter = decoding(CrEncodeInter, width / 2, height / 2);

            YDCTInter = invQuantization(YQuantInter);
            CbDCTInter = invQuantization(CbQuantInter);
            CrDCTInter = invQuantization(CrQuantInter);

            YDif = inverseDCTInter(YDCTInter);
            CbDif = inverseDCTInter(CbDCTInter);
            CrDif = inverseDCTInter(CrDCTInter);

            YInter = unCalcDifference(Y, YMoVec, YDif);
            CbInter = unCalcDifference(Cb, CbMoVec, CbDif);
            CrInter = unCalcDifference(Cr, CrMoVec, CrDif);
            
            unsubSampling(ref CbInter, ref CrInter);
            YCbCrtoRGB(ret[1], YInter, CbInter, CrInter);


            return ret;
        }

        public void save(Stream stream)
        {
            BinaryWriter binaryWriter = new BinaryWriter(stream);

            binaryWriter.Write(id);

            binaryWriter.Write(width);
            binaryWriter.Write(height);

            // intra y
            binaryWriter.Write(YEncode.Count);
            binaryWriter.Write(YEncode.ToArray());
            binaryWriter.Write(CbEncode.Count);
            binaryWriter.Write(CbEncode.ToArray());
            binaryWriter.Write(CrEncode.Count);
            binaryWriter.Write(CrEncode.ToArray());

            //inter frame
            binaryWriter.Write(YEncodeInter.Count);
            binaryWriter.Write(YEncodeInter.ToArray());
            binaryWriter.Write(CbEncodeInter.Count);
            binaryWriter.Write(CbEncodeInter.ToArray());
            binaryWriter.Write(CrEncodeInter.Count);
            binaryWriter.Write(CrEncodeInter.ToArray());


            // inter y motion vector
            binaryWriter.Write(YMoVec.GetLength(0));
            binaryWriter.Write(YMoVec.GetLength(1));
            for (int x = 0; x < YMoVec.GetLength(0); x++)
            {
                for (int y = 0; y < YMoVec.GetLength(1); y++)
                {
                    binaryWriter.Write(YMoVec[x, y].X);
                    binaryWriter.Write(YMoVec[x, y].Y);
                }
            }

            binaryWriter.Write(CbMoVec.GetLength(0));
            binaryWriter.Write(CbMoVec.GetLength(1));
            for (int x = 0; x < CbMoVec.GetLength(0); x++)
            {
                for (int y = 0; y < CbMoVec.GetLength(1); y++)
                {
                    binaryWriter.Write(CbMoVec[x, y].X);
                    binaryWriter.Write(CbMoVec[x, y].Y);
                }
            }
            binaryWriter.Write(CrMoVec.GetLength(0));
            binaryWriter.Write(CrMoVec.GetLength(1));
            for (int x = 0; x < CrMoVec.GetLength(0); x++)
            {
                for (int y = 0; y < CrMoVec.GetLength(1); y++)
                {
                    binaryWriter.Write(CrMoVec[x, y].X);
                    binaryWriter.Write(CrMoVec[x, y].Y);
                }
            }
            binaryWriter.Close();
        }

    }
}
