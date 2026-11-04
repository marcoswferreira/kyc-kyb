using Domain.Enums;

namespace Application.Interfaces;

public interface IDataMasker
{
    string MaskDocument(string documentNumber, UserRole role);
    string MaskName(string name, UserRole role);
}

