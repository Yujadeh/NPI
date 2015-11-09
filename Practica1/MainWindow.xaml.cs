//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------


/****************************************
 * 
 * Autor: Jose Antonio España Valdivia
 * Correo: jaespana@correo.ugr.es
 * Fecha última modificación: 30-10-2015
 * 
 *****************************************/

/*Enumerador para poder indicar la posición en la que se encuentra
 * - Mal. La posición es incorrecta y se realiza el movimiento desde el principio
 * - Postura_Inicial. Variable por defecto al captar los puntos. Piernas y brazos rectos.
 * - Sigue_Bajando. Indica que el usuario aun no ha ejecutado el primer moviento de agacharse
 * - Agachado. Indica que el usuario ha finalizado el movimiento de agacharse y ha de proceder a levantar los brazos
 * - Final. Indica que el usuario ha finaliza el flujo de movimientos.
 */
public enum posturas { Mal, Postura_Inicial, Sigue_Bajando, Agachado, Final };

namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Kinect;
    using System.Windows.Media.Imaging;
    using System;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        //Variable para guardar la distancia entre hombro y codo captdos
        private float distancia_hombro_codo = 0;

        //Variable para guardar la distancia entre hombro y muñeca captados
        private float distancia_hombro_muñeca = 0;

        //Variable que indica la distancia que hemos de bajar en el primer movimiento
        private float dist_agache = 0.30f;

        //Tolerancia dada a los movimientos del problema
        private double tolerancia = 0.125f;

        //Variables para controlar un pequeño contador para que los movimientos no sean instantaneos
        private int numero_frame = 0;
        private int frame_aux = 0;
        //Variable que activará el contador para captar los movimientos
        private bool contador_inicial = false;

        //Variable lógica para indicar si pintamos las esferas de ayuda de color correcto o incorrecto
        private bool pinta_correcto = false;

        //Variable tipo enumador para verificar en que postura estamos según el esqueleto captado por el Kinect
        posturas posturaActual = posturas.Postura_Inicial;

        //Variable para guardar la altura de la cabeza en la posición inicial captada por el Kinect
        float altura_cabeza;

        //Variable para guardar la altura de la cabeza una vez finalizado el primer moviemto (agacharse)
        float altura_cabeza_agachado;


        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;
        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        private WriteableBitmap colorBitmap;

        /// <summary>
        /// Intermediate storage for the color data received from the camera
        /// </summary>
        private byte[] colorPixels;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary >        
        private readonly Brush inferredJointBrush = Brushes.Red;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

                /************************************************************/
        /*****************************METODOS PROPIOS DE KINECT*******************************/
                /************************************************************/
        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
        InitializeComponent();
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {

                this.Esqueleto.Source = this.imageSource;
                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();
                
                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;
                // Turn on the color stream to receive color frames
                this.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

                // Allocate space to put the pixels we'll receive
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

                // This is the bitmap we'll display on-screen
                this.colorBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                // Set the image we display to point to the bitmap where we'll put the image data
                this.ColorI.Source = this.colorBitmap;

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.ColorFrameReady += this.SensorColorFrameReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

           
            }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
            {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
            }

        /// <summary>
        /// Event handler for Kinect sensor's ColorFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>  
       private void SensorColorFrameReady(object sender, ColorImageFrameReadyEventArgs e){
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame()){
                if (colorFrame != null){
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(this.colorPixels);

                    // Write the pixel data into our bitmap
                    this.colorBitmap.WritePixels(
                        new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                        this.colorPixels,
                        this.colorBitmap.PixelWidth * sizeof(int),
                        0);
                }
            }
        }

       /// <summary>
       /// Event handler for Kinect sensor's SkeletonFrameReady event
       /// </summary>
       /// <param name="sender">object sending the event</param>
       /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                    numero_frame = skeletonFrame.FrameNumber;
                    if(contador_inicial == false){
                        frame_aux = numero_frame;
                        contador_inicial = true;
                    }
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.DrawBonesAndJoints(skel, dc);
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }
            
                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext){
            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);
          
            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);

            // Comprueba los movimientos y dibuja la ayuda 

            Indicaciones(skeleton,drawingContext);
            CompruebaPostura(skeleton);


            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null; 

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;                    
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;                    
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint){
                // Convert point to depth space.  
                // We are not using depth directly, but we do want the points in our 640x480 output resolution.

                DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
                return new Point(depthPoint.X, depthPoint.Y);
            }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1){
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = this.trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }



            /************************************************************/
        /***********METODOS PRINCIPALES PARA CAPTAR EL MOVIMIENTO****************/
            /************************************************************/

        /// <summary>
        /// Metodo general para comprobar la postura
        /// </summary>
        /// <param name="esqueleto"></param>
        /// <returns></returns>
        public void CompruebaPostura(Skeleton esqueleto){    
                if (posturaActual == posturas.Postura_Inicial){
                    altura_cabeza = esqueleto.Joints[JointType.Head].Position.Y;
                    captarDistancias(esqueleto);
                }
                else if (posturaActual == posturas.Sigue_Bajando){
                    this.Mensaje.Text = "\t Baja hasta la posición indicada";
                    Agachado(esqueleto);
                }
                else if (posturaActual == posturas.Agachado){
                    this.Mensaje.Text = "\t Coloque los brazos hacia arriba";
                    comprobarSubida(esqueleto);
                    ManosArriba(esqueleto);
                }
                else if (posturaActual == posturas.Final){
                    this.Mensaje.Text = "\t Se acabo el ejercicio";
                    posturaActual = posturas.Postura_Inicial;
                }
                else if (posturaActual == posturas.Mal) {
                    this.Mensaje.Text = "\t ERROR: Empieza de nuevo";
                    posturaActual = posturas.Postura_Inicial;
                }
                
        }

        /// <summary>
        /// Metodo para guiar al usuario
        /// </summary>
        /// <param name="esqueleto"></param>
        /// <param name="drawingContext"></param>
        /// <returns></returns>
        public void Indicaciones(Skeleton esqueleto, DrawingContext drawingContext){
                
                if (posturaActual == posturas.Sigue_Bajando){
                    posicionamientoAgache(esqueleto,drawingContext);
                }
                else if (posturaActual == posturas.Agachado)
                {
                    posicionamientoManos(esqueleto, drawingContext);
                }
            }

        /// <summary>
        /// Metodo para comprobar si tenemos las manos alzadas
        /// </summary>
        /// <param name="esqueleto"></param>
        /// <returns></returns>
        public void ManosArriba(Skeleton esqueleto){
            
            Joint muñecaDerecha = esqueleto.Joints[JointType.WristRight];
            Joint hombroDerecho = esqueleto.Joints[JointType.ShoulderRight];
            Joint codoDerecho = esqueleto.Joints[JointType.ElbowRight];
            Joint muñecaIzquierda = esqueleto.Joints[JointType.WristLeft];
            Joint hombroIzquierdo = esqueleto.Joints[JointType.ShoulderLeft];
            Joint codoIzquierdo = esqueleto.Joints[JointType.ElbowLeft];

            /*Pasos de comprobación:
             *  - Compruebo que la muñeca y el codo estén en la misma recta vertical 
             *  - Compruebo que el hombro y el codo estén en la misma recta vertical 
             *  - Compruebo que la muñeca esté por encima del codo 
             *  - Compruebo que el codo esté por encima del hombro
             *  - Compruebo que no tengo la muñeca inclinada(en profundidad) respecto al codo(o viceversa)
             *  - Compruebo que no tengo el codo inclinado(en profundidad) respecto al hombro (o viceversa)
             */
            if (        muñecaDerecha.Position.Y > codoDerecho.Position.Y &&
                        codoDerecho.Position.Y > hombroDerecho.Position.Y &&
                        Math.Abs(muñecaDerecha.Position.X - codoDerecho.Position.X) < this.tolerancia && 
                        Math.Abs(hombroDerecho.Position.X - codoDerecho.Position.X) < (this.tolerancia) &&
                        Math.Abs(muñecaDerecha.Position.Z - codoDerecho.Position.Z) < this.tolerancia*3 &&
                        Math.Abs(hombroDerecho.Position.Z - codoDerecho.Position.Z) < (this.tolerancia) &&

                        muñecaIzquierda.Position.Y > codoIzquierdo.Position.Y &&
                        codoIzquierdo.Position.Y > hombroIzquierdo.Position.Y &&
                        Math.Abs(muñecaIzquierda.Position.X - codoIzquierdo.Position.X) < this.tolerancia &&
                        Math.Abs(muñecaIzquierda.Position.Z - codoIzquierdo.Position.Z) < this.tolerancia &&
                        Math.Abs(hombroIzquierdo.Position.X - codoIzquierdo.Position.X) < this.tolerancia &&
                        Math.Abs(hombroIzquierdo.Position.Z - codoIzquierdo.Position.Z) < this.tolerancia*3)  
            {//then
                if (pinta_correcto == false){
                    pinta_correcto = true;
                    frame_aux = numero_frame;
                }
                else if (frame_aux + 30 < numero_frame){
                        posturaActual = posturas.Final;
                        pinta_correcto = false;
                    
                }
            }else if (pinta_correcto == true)
                    pinta_correcto = false;  
        }

        /// <summary>
        /// Metodo para comprobar si estamos agachados
        /// </summary>
        /// <param name="esqueleto"></param>
        /// <returns></returns>
        public void Agachado(Skeleton esqueleto){
            
            Joint cabeza = esqueleto.Joints[JointType.Head];

            /*Pasos de comprobación:
             *  - Compruebo que la posición de la cabeza está a una determinada altura
             */
            if (altura_cabeza - cabeza.Position.Y > 0.30 ) {//then
                if (pinta_correcto == false) {
                    altura_cabeza_agachado = cabeza.Position.Y;
                    pinta_correcto = true;
                    frame_aux = numero_frame;
                }
                else if (frame_aux + 30 < numero_frame){
                    posturaActual = posturas.Agachado;
                    pinta_correcto = false;
                }
            }
            else if (pinta_correcto == true)
                pinta_correcto = false;
       
        }

        /// <summary>
        /// Metodo para comprobar si se sube una vez agachado
        /// </summary>
        /// <param name="esqueleto"></param>
        /// <returns></returns>
        public void comprobarSubida(Skeleton esqueleto)
        {
            this.Mensaje.Text = "\t Baja hasta la posición indicada";
            Joint cabeza = esqueleto.Joints[JointType.Head];

            /*Pasos de comprobación:
             *  - Compruebo que la posición de la cabeza está a una determinada altura
             */
            if (cabeza.Position.Y > altura_cabeza_agachado+0.1) {//then
                posturaActual = posturas.Mal;
            }
        }

        /// <summary>
        /// Metodo de ayuda al usuario para agacharse
        /// </summary>
        /// <param name="esqueleto"></param>
        /// <param name="drawingContext"></param>
        /// <returns></returns>
        private void posicionamientoAgache(Skeleton esqueleto, DrawingContext drawingContext){
            //Puntos que indican donde se deben situar las manos y los codos.
            SkeletonPoint cabeza = new SkeletonPoint();
            cabeza.X = esqueleto.Joints[JointType.Head].Position.X;
            cabeza.Y = altura_cabeza - dist_agache;
            cabeza.Z = esqueleto.Joints[JointType.Head].Position.Z;

            if (pinta_correcto == false){
                Point pos1 = this.SkeletonPointToScreen(cabeza);
                drawingContext.DrawEllipse(inferredJointBrush, null, pos1, 15, 15);
                
            }
            else{
                Point pos1 = this.SkeletonPointToScreen(cabeza);
                drawingContext.DrawEllipse(centerPointBrush, null, pos1, 15, 15); 
            }

        }

        /// <summary>
        /// Metodo de ayuda al usuario para levantar brazos
        /// </summary>
        /// <param name="esqueleto"></param>
        /// <param name="drawingContext"></param>
        /// <returns></returns>
        private void posicionamientoManos(Skeleton esqueleto, DrawingContext drawingContext){
            //Puntos que indican donde se deben situar las manos y los codos.
            SkeletonPoint manoDerecha = new SkeletonPoint();
            SkeletonPoint codoDerecha = new SkeletonPoint();
            SkeletonPoint manoIzquierda = new SkeletonPoint();
            SkeletonPoint codoIzquierda = new SkeletonPoint();

            // X sera la distancia euclidea entre la muñeca y el hombro
            manoDerecha.X = esqueleto.Joints[JointType.ShoulderLeft].Position.X;
            manoDerecha.Y = esqueleto.Joints[JointType.ShoulderLeft].Position.Y + distancia_hombro_muñeca;
            manoDerecha.Z = esqueleto.Joints[JointType.ShoulderLeft].Position.Z;

            // X sera la distancia euclidea entre el codo y el hombro
            codoDerecha.X = esqueleto.Joints[JointType.ShoulderLeft].Position.X;
            codoDerecha.Y = esqueleto.Joints[JointType.ShoulderLeft].Position.Y + distancia_hombro_codo;
            codoDerecha.Z = esqueleto.Joints[JointType.ShoulderLeft].Position.Z;


            //La X sera la distancia euclidea entre la muñeca y el hombro
            manoIzquierda.X = esqueleto.Joints[JointType.ShoulderRight].Position.X;
            manoIzquierda.Y = esqueleto.Joints[JointType.ShoulderRight].Position.Y + distancia_hombro_muñeca;
            manoIzquierda.Z = esqueleto.Joints[JointType.ShoulderRight].Position.Z;

            // X sera la distancia euclidea entre el codo y el hombro
            codoIzquierda.X = esqueleto.Joints[JointType.ShoulderRight].Position.X;
            codoIzquierda.Y = esqueleto.Joints[JointType.ShoulderRight].Position.Y + distancia_hombro_codo;
            codoIzquierda.Z = esqueleto.Joints[JointType.ShoulderRight].Position.Z;

            if (pinta_correcto == false){
                Point pos1 = this.SkeletonPointToScreen(manoDerecha);
                drawingContext.DrawEllipse(inferredJointBrush, null, pos1, 15, 15);
                Point pos2 = this.SkeletonPointToScreen(codoDerecha);
                drawingContext.DrawEllipse(inferredJointBrush, null, pos2, 15, 15);
                Point pos3 = this.SkeletonPointToScreen(manoIzquierda);
                drawingContext.DrawEllipse(inferredJointBrush, null, pos3, 15, 15);
                Point pos4 = this.SkeletonPointToScreen(codoIzquierda);
                drawingContext.DrawEllipse(inferredJointBrush, null, pos4, 15, 15);
            }
            else{
                Point pos1 = this.SkeletonPointToScreen(manoDerecha);
                drawingContext.DrawEllipse(centerPointBrush, null, pos1, 15, 15);
                Point pos2 = this.SkeletonPointToScreen(codoDerecha);
                drawingContext.DrawEllipse(centerPointBrush, null, pos2, 15, 15);
                Point pos3 = this.SkeletonPointToScreen(manoIzquierda);
                drawingContext.DrawEllipse(centerPointBrush, null, pos3, 15, 15);
                Point pos4 = this.SkeletonPointToScreen(codoIzquierda);
                drawingContext.DrawEllipse(centerPointBrush, null, pos4, 15, 15);
            }

        }

        /// <summary>
        /// Metodo auxiliar para captar las distancias a usar
        /// </summary>
        /// <param name="esqueleto"></param>
        /// <returns></returns>
        public void captarDistancias(Skeleton esqueleto){

            this.Mensaje.Text = "\t Captando puntos. Espere porfavor";

            if (frame_aux + 240 > numero_frame){
                if (distancia_hombro_codo < distanciaEuclidea(esqueleto.Joints[JointType.ElbowLeft], esqueleto.Joints[JointType.ShoulderLeft]))
                    distancia_hombro_codo = distanciaEuclidea(esqueleto.Joints[JointType.ElbowLeft], esqueleto.Joints[JointType.ShoulderLeft]);

                if (distancia_hombro_muñeca < distanciaEuclidea(esqueleto.Joints[JointType.WristLeft], esqueleto.Joints[JointType.ShoulderLeft]))
                    distancia_hombro_muñeca = distanciaEuclidea(esqueleto.Joints[JointType.WristLeft], esqueleto.Joints[JointType.ShoulderLeft]);
            }
            else
                posturaActual = posturas.Sigue_Bajando;

        }
        /// <summary>
        /// Metodo auxiliar para calcular distancias entre dos puntos
        /// </summary>
        /// <param name="p"></param>
        /// <param name="q"></param>
        /// <returns></returns>
        public float distanciaEuclidea(Joint p, Joint q){
            double pX = p.Position.X;
            double pY = p.Position.Y;
            double pZ = p.Position.Z;

            double qX = q.Position.X;
            double qY = q.Position.Y;
            double qZ = q.Position.Z;

            return (float)Math.Sqrt(Math.Pow(qX - pX, 2) + Math.Pow(qY - pY, 2) + Math.Pow(qZ - pZ, 2));
        }
    }
}