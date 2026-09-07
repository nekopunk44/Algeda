using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Primitives
{
    public enum ActivityType
    {
        Undefined = 0,          // неопределенное действие
        PropertyShowing = 1,    // показ объекта
        ClientCall = 2,         // звонок клиенту
        StatusUpdate = 3,       // обновление статуса в системе
        PropertyListing = 4,    // размещение нового объекта
        DealClosure = 5,        // закрытие сделки
        DealCancellation = 6    // отмена сделки
    }
}
