namespace CuandoNadieMira.Menu
{
    /// <summary>Textos para el jugador según lo que reporta el detector. Un solo lugar para que todas las pantallas digan lo mismo.</summary>
    public static class TrackingText
    {
        /// <summary>Qué pasa, en una frase corta.</summary>
        public static string Problem(EyeTrackingService s)
        {
            if (s == null) return "El detector no está encendido.";
            switch (s.State)
            {
                case TrackingState.MissingExe: return "Falta el detector de ojos (ojos_server).";
                case TrackingState.PortBusy: return "Otro programa está usando el detector.";
                case TrackingState.ProcessExited: return "El detector de ojos se cerró.";
                case TrackingState.NoSignal: return "El detector dejó de responder.";
                case TrackingState.Off:
                case TrackingState.Starting: return "Encendiendo la cámara...";
            }
            return s.Last.reason switch
            {
                "ok" => "Te vemos bien.",
                "sin_rostro" => "No se detecta tu rostro.",
                "rostro_pequeno" => "Tu rostro se ve muy pequeño.",
                "varios_rostros" => "Hay más de una persona frente a la cámara.",
                "rostro_cortado" => "Tu rostro queda cortado en el borde.",
                "banda_fuera" => "No se ven bien tus ojos.",
                "camara_no_disponible" => "No encontramos ninguna cámara.",
                "camara_sin_imagen" => "La cámara no entrega imagen.",
                _ => "Señal inestable.",
            };
        }

        /// <summary>Qué hacer para arreglarlo.</summary>
        public static string Advice(EyeTrackingService s)
        {
            if (s == null) return "";
            switch (s.State)
            {
                case TrackingState.MissingExe: return "Descomprime ojos_server.zip en Assets/StreamingAssets/ y vuelve a intentar.";
                case TrackingState.PortBusy: return "Cierra el otro juego o la otra ventana de Unity que use la cámara y reintenta.";
                case TrackingState.ProcessExited: return "Revisa que la cámara esté conectada y no la use otra aplicación. Luego reintenta.";
                case TrackingState.NoSignal: return "Reintenta. Si sigue igual, reinicia el juego.";
                case TrackingState.Off:
                case TrackingState.Starting: return "Esto tarda unos segundos la primera vez.";
            }
            return s.Last.reason switch
            {
                "ok" => "Mantente así.",
                "sin_rostro" => "Siéntate frente a la cámara y mira hacia ella.",
                "rostro_pequeno" => "Acércate un poco a la cámara.",
                "varios_rostros" => "Quédate solo frente a la cámara.",
                "rostro_cortado" => "Centra tu cara dentro del marco.",
                "banda_fuera" => "Endereza la cabeza y no te tapes los ojos.",
                "camara_no_disponible" => "Conecta una cámara o revisa los permisos de cámara de Windows.",
                "camara_sin_imagen" => "Cierra otras aplicaciones que usen la cámara y reintenta.",
                _ => "Evita giros bruscos y busca más luz sobre tu cara.",
            };
        }

        public static string ConfidenceLabel(float score) =>
            score >= 0.85f ? "alta" : score >= 0.65f ? "media" : score > 0f ? "baja" : "sin datos";
    }
}
