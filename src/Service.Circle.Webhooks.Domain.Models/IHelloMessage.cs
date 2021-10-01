using System;

namespace Service.Circle.Webhooks.Domain.Models
{
    public interface IHelloMessage
    {
        string Message { get; set; }
    }
}
