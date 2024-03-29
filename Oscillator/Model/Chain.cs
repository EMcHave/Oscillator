﻿using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Complex;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Input;

namespace Oscillator.Model
{
    internal class Chain : INotifyPropertyChanged
    {
        public List<Particle> Particles { get; set; }
        private List<Particle> ConstrainedParticles;

        public Layout Layout;
        public ForceType ForceType;

        private double c;
        public double C {
            get { return c; }
            set 
            { 
                c = value;
                forceCoef = 0;
                NotifyPropertyChanged("ForceCoef"); 
            } 
        }
        private double mass;
        public double m
        {
            get { return mass; }
            set
            {
                mass = value;
                forceCoef = 0;
                NotifyPropertyChanged("ForceCoef");
            }
        }
        public double mu { get; set; }

        private double t;
        
        public double T
        {
            get { return t / mulT; }
            set { t = value * mulT; }
        }
        public double mulT { get { return Math.Sqrt(g / dx); } }
        public int nT { get; private set; }
        public double dt { get; private set; }

        private double forceCoef;
        public double ForceCoef { 
            get
            { 
                if (forceCoef == 0) 
                    return C * dx / (m * g);
                else 
                    return forceCoef;
            }
            set 
            { 
                if (value != C * dx / (m * g))
                    forceCoef = value; 
            }
        }

        public double DimCoef { get { return H / dx; } }

        private int nx;
        public int nX
        { 
            get
            {
                return nx;
            }
            set
            {
                nx = value;
                h = 0;
                NotifyPropertyChanged(nameof(H));
            }
        }
        public double dx
        { get { return 1.0 / (nX - 1); } }

        private double h;
        public double H
        {
            get
            {
                if (nY > 1)
                    if (h == 0)
                        return (nY - 1) * dx;
                    else
                        return h;
                else
                    return 0;
            }
            set
            {
                if (value != (nY - 1) * dx)
                    h = value;
                NotifyPropertyChanged(nameof(DimCoef));
                NotifyPropertyChanged(nameof(H));
            }
        }
        public int nY { get; set; }
        public double dy { get
            {
                if (nY > 1)
                    return H / (nY - 1);
                else return 0;
            }
        }
        public int N { get { return nX * nY; } }


        public const double g = 9.81;
        private readonly Vector<double> gk = Vector<double>.Build.DenseOfArray(new double[] { 0, -9.81 });
        private readonly Vector<double> gk_norm = Vector<double>.Build.DenseOfArray(new double[] { 0, -1.0 });

        public delegate void Draw(object sender, DrawEventArgs e);
        public event Draw drawEvent;
        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Chain()
        {
            C = 10000;
            m = 0.1;
            mu = 0;
            nX = 20;
            nY = 2;
            T = 5;
            H = (nY - 1) * dx;
        }

        private void SetConstraints(bool isFree)
        {
            switch(this.Layout)
            {
                case Layout.Horizontal:
                    for (int i = 0; i < nY; i++)
                    {
                        ConstrainedParticles.Add(Particles[i * nX]);
                        ConstrainedParticles.Add(Particles[nX * (i + 1) - 1]);
                    }
                    break;
                case Layout.Vertical:
                    for (int i = 0; i < nY; i++)
                    {
                        
                        if ((i * nX) % (2 * nX) == 0)
                            ConstrainedParticles.Add(Particles[i * nX]);
                        if (i % 2 != 0)
                            ConstrainedParticles.Add(Particles[nX * (i + 1) - 1]);
                        
                    }
                    break;
            }

            if (isFree)
            { ConstrainedParticles.Clear(); ConstrainedParticles.Add(Particles[0]); }
        }
        private void ApplyConstraints(double[] a,
                                      double[] b,
                                      double[] c,
                                      Vector<double>[] f)
        {
            foreach(Particle p in ConstrainedParticles)
            {
                a[p.ID] = 0;
                b[p.ID] = 1;
                c[p.ID] = 0;
                f[p.ID][0] = 0;
                f[p.ID][1] = 0;
            }
        }

        public void StaticStep(double userDefDt)
        {
            
            double DT = 0.1 / Math.Sqrt(ForceCoef);

            int delta = (int)(t / DT) % (int)(60 * t);
            dt = t / ((int)(t / DT) + 60 * t - delta);
            if (!Double.IsNaN(userDefDt))
                dt = userDefDt;


            Particles = new List<Particle>(N);
            ConstrainedParticles = new List<Particle>();
            CreateParticles();
            SetConstraints(false);


            double[] a = new double[N];
            double[] b = new double[N];
            double[] c = new double[N];
            Vector<double>[] f = new Vector<double>[N];


            double[] C_i = new double[N - 1];
            List<Vector<double>> tempR;
            List<Vector<double>> resR = new List<Vector<double>>(N);
            Vector<double>[] resV = new Vector<double>[N];

            foreach (Particle p in Particles)
            {
                resR.Add(p.R[0]);
                resV[p.ID] = p.V[0];
            }

            double dtau = dt;
            //double m = Particles[0].m;

            double eps = Math.Pow(10, -10);
            double D = 1;

            double MU = mu;
            mu = 0.1;

            while (D > eps)
            {              
                tempR = resR.ToList<Vector<double>>();

                TimeStep(a, b, c, f, C_i, ref resR, ref resV, dtau);

                for (int i = 0; i < resR.Count; i++)
                {
                    resR[i] = tempR[i] + dtau * resV[i];
                }
                D = resV.Max(x => x.AbsoluteMaximum());
            }
            Vector<double> A = Vector<double>.Build.DenseOfArray(new double[] { 0, 0 });
            foreach (Particle p in Particles)
            {
                p.R.Add(resR[p.ID]);
                p.V.Add(resV[p.ID]);
                p.A.Add(A);
            }

            mu = MU;
        }

        async public void DynamicStep()
        {
            double[] a = new double[N];
            double[] b = new double[N];
            double[] c = new double[N];
            Vector<double>[] f = new Vector<double>[N];

            double[] C_i = new double[N - 1];


            SetConstraints(true);

            List<Vector<double>> resR = new List<Vector<double>>(Particles.Count);
            Vector<double>[] resV = new Vector<double>[N];
            foreach (Particle p in Particles)
            {
                resR.Add(p.R[1]);
                resV[p.ID] = p.V[1];
            }


            nT = (int)(t / dt);
            for (int n = 2; n < nT; n++)
            {
                TimeStep(a, b, c, f, C_i, ref resR, ref resV, dt);

                foreach (Particle p in Particles)
                {
                    p.V.Add(resV[p.ID]);
                    p.R.Add(p.R[n - 1] + dt * p.V[n]);
                    p.A.Add((p.V[n] - p.V[n - 1]) / dt);
                    resR[p.ID] = p.R[n];
                }
            }

            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
            () =>
            {
                drawEvent(this, new DrawEventArgs { whatToDraw = "Перемещения" });
            });         
        }

        static private Vector<double>[] ThomasAlg(double[] a,
                                                  double[] b,
                                                  double[] c,
                                                  Vector<double>[] f)
        {
            double[] delta = new double[a.Length];
            Vector<double>[] lamdba = new Vector<double>[a.Length];
            Vector<double>[] solution = new Vector<double>[a.Length];

            delta[0] = -a[0] / b[0];
            lamdba[0] = f[0] / b[0];
            double denom;
            int N1 = a.Length - 1;
            for (int i = 1; i < N1; i++)
            {
                denom = b[i] + a[i] * delta[i - 1];
                delta[i] = -c[i] / denom;
                lamdba[i] = (f[i] - a[i] * lamdba[i - 1]) / denom;
            }

            solution[N1] = (f[N1] - a[N1] * lamdba[N1 - 1])/
                (b[N1] + a[N1] * delta[N1-1]);

            for (int i = N1 - 1; i>=0; i--)
                solution[i] = delta[i] * solution[i+1] + lamdba[i];

            return solution;
        }

        private void TimeStep(double[] a, double[] b, double[] c, Vector<double>[] f,
                                double[] C_i, ref List<Vector<double>> resR, ref Vector<double>[] resV,
                                double dtau)
        {
            double m = Particles[0].m;

            /// жесткости на k + 1 итерации ///
            if (ForceType == ForceType.JustExtension)
            {
                for (int i = 0; i < N - 1; i++)
                    if ((resR[i + 1] - resR[i]).L2Norm() > 1)
                        C_i[i] = ((resR[i + 1] - resR[i]).L2Norm() - 1) /
                                 (resR[i + 1] - resR[i]).L2Norm() * ForceCoef;
                    else C_i[i] = 0;
            }
            else if (ForceType == ForceType.CompressionExtension)
            {
                for (int i = 0; i < N - 1; i++)
                    C_i[i] = ((resR[i + 1] - resR[i]).L2Norm() - 1) /
                             (resR[i + 1] - resR[i]).L2Norm() * ForceCoef;
            }




            /// начала и концы массивов a, b, c,  f ///
            a[0] = 0;
            a[N - 1] = -C_i[N - 2];

            c[0] = -C_i[0];
            c[N - 1] = 0;

            b[0] = C_i[0] + 1 / dtau / dtau + mu / dtau;
            b[N - 1] = C_i[N - 2] + 1 / dtau / dtau + mu / dtau;

            f[0] = 1 / dtau * (resV[0] / dtau + C_i[0] * resR[1] - (C_i[0] + 0) * resR[0] + gk_norm);
            f[N - 1] = 1 / dtau * (resV[N - 1] / dtau - (C_i[N - 2] + 0) * resR[N - 1]
                    + C_i[N - 2] * resR[N - 2] + gk_norm);

            /// заполнение массивов a, b, c, f \\\
            for (int i = 1; i < N - 1; i++)
            {
                a[i] = -C_i[i - 1];
                b[i] = (C_i[i - 1] + C_i[i]) + 1 / dtau / dtau + mu / dtau;
                c[i] = -C_i[i];
                f[i] = 1 / dtau * (resV[i] / dtau + C_i[i] * resR[i + 1] - (C_i[i] + C_i[i - 1]) * resR[i]
                    + C_i[i - 1] * resR[i - 1] + gk_norm);
            }

            /// задание закреплений ///
            ApplyConstraints(a, b, c, f);

            /// прогонка - вычисление V^(k+1) ///
            resV = ThomasAlg(a, b, c, f);
        }

        private void CreateParticles()
        {
            Vector<double> v = Vector<double>.Build.DenseOfArray(new double[] { 0, 0 });
            Vector<double> a = Vector<double>.Build.DenseOfArray(new double[] { 0, 0 });

            switch (this.Layout)
            {
                case Layout.Horizontal:
                    for (int n = 0; n < nY; n++)
                    {
                        if (n % 2 == 0)
                            for (int i = 0; i < nX; i++)
                                Particles.Add(new Particle(
                                    Vector<double>.Build.DenseOfArray(new double[] { i ,  - n }),
                                    v, a, m, Particles.Count, nT));
                        else
                            for (int i = nX - 1; i >= 0; i--)
                                Particles.Add(new Particle(
                                    Vector<double>.Build.DenseOfArray(new double[] { i, -n }),
                                    v, a, m, Particles.Count, nT));
                    }
                    break;
                case Layout.Vertical:
                    for (int n = 0; n < nY; n++)
                    {
                        if (n % 2 == 0)
                            for (int i = 0; i < nX; i++)
                                Particles.Add(new Particle(
                                    Vector<double>.Build.DenseOfArray(new double[] {dy / dx * n , - i }),
                                    v, a, m, Particles.Count, nT));
                        else
                            for (int i = nX - 1; i >= 0; i--)
                                Particles.Add(new Particle(
                                    Vector<double>.Build.DenseOfArray(new double[] {dy / dx * n, - i }),
                                    v, a, m, Particles.Count, nT));
                    }
                    break;
            }
        }
    }

    class DrawEventArgs : EventArgs
    {
        public string whatToDraw { get; set; }
    }

    enum Layout
    {
        Horizontal,
        Vertical
    }

    enum ForceType
    {
        CompressionExtension,
        JustExtension
    }
}
