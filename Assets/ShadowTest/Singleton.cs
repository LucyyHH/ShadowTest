public class Singleton<T> where T : new() {
    private static T s_ist;
    public static T GetInstance () {
        if (s_ist == null) {
            s_ist = new T ();
        }

        return s_ist;
    }

    public static bool CheckInstance () {
        return s_ist != null;
    }
}
