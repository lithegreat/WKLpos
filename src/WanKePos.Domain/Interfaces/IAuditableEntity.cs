using System;

namespace WanKePos.Domain.Interfaces;

/// <summary>
/// 标记具有审计时间戳的实体
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    DateTime LastModified { get; set; }
}
