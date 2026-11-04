using Application.Interfaces;
using Domain.Enums;
using Domain.ValueObjects;

namespace Infrastructure.Security;

public class DataMasker : IDataMasker
{
    public string MaskDocument(string documentNumber, UserRole role)
    {
        if (role is UserRole.Admin or UserRole.ComplianceOfficer)
        {
            return documentNumber;
        }

        if (documentNumber.Length == 11)
        {
            var cpfRes = Cpf.Create(documentNumber);
            return cpfRes.IsSuccess ? cpfRes.Value!.Masked : "***.***.***-**";
        }

        var cnpjRes = Cnpj.Create(documentNumber);
        return cnpjRes.IsSuccess ? cnpjRes.Value!.Masked : "**.***.***/****-**";
    }

    public string MaskName(string name, UserRole role)
    {
        if (role is UserRole.Admin or UserRole.ComplianceOfficer)
        {
            return name;
        }

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1)
        {
            return name.Length > 2 ? $"{name[..2]}***" : "***";
        }

        return $"{parts[0]} " + string.Join(" ", parts.Skip(1).Select(p => $"{p[0]}***"));
    }
}

