namespace IEDCollector.Lib
{
    internal interface IParsable<T>
    {
        T Parse(object o);
    }
}
