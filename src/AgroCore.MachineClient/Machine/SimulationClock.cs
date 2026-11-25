namespace AgroCore.MachineClient.Machine
{
    /// <summary>
    /// Simulazioaren abiadura (Kontrol Zentroko x1 / x2 / x5 / x10 botoiak).
    ///
    /// Makinak beti 20 urrats egiten ditu segundoko (denbora errealean), baina urrats bakoitzean
    /// "Speed" aldiz denbora gehiago simulatzen du: x10 abiaduran, segundo erreal bakoitzean
    /// 10 segundoko mugimendua, bateria-kontsumoa eta karga egiten dira.
    ///
    /// Sareko irakurle-hariak idazten du (SET_SIMULATION_SPEED jasotzean) eta simulazio-hariak irakurtzen du:
    /// volatile nahikoa da, int bakarra delako.
    /// </summary>
    public static class SimulationClock
    {
        private static volatile int speed = 1;

        public static int Speed
        {
            get { return speed; }
            set
            {
                int value2 = value;
                if (value2 < 1)
                {
                    value2 = 1;
                }
                if (value2 > 10)
                {
                    value2 = 10;
                }
                speed = value2;
            }
        }

        /// <summary>Denbora errealeko segundo kopuru bat simulatutako segundo bihurtzen du.</summary>
        public static double Scale(double realSeconds)
        {
            return realSeconds * speed;
        }
    }
}
