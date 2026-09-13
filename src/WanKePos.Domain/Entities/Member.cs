using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WanKePos.Domain.Enums;
using WanKePos.Domain.Interfaces;

namespace WanKePos.Domain.Entities;

/// <summary>
/// 会员实体 (实现 INotifyPropertyChanged 确保 UI 绑定在属性变更时毫秒级自动响应刷新)
/// </summary>
public class Member : IAuditableEntity, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private int _id;
    public int Id { get => _id; set => SetField(ref _id, value); }
    
    private string _memberNo = string.Empty;
    public string MemberNo { get => _memberNo; set => SetField(ref _memberNo, value); }
    
    private string _phone = string.Empty;
    public string Phone { get => _phone; set => SetField(ref _phone, value); }
    
    private string _name = string.Empty;
    public string Name { get => _name; set => SetField(ref _name, value); }
    
    private string? _gender;
    public string? Gender { get => _gender; set => SetField(ref _gender, value); }
    
    private DateTime? _birthday;
    public DateTime? Birthday { get => _birthday; set => SetField(ref _birthday, value); }
    
    private DateTime _registerTime;
    public DateTime RegisterTime { get => _registerTime; set => SetField(ref _registerTime, value); }
    
    private string? _storeName;
    public string? StoreName { get => _storeName; set => SetField(ref _storeName, value); }
    
    private string? _bpin;
    public string? Bpin { get => _bpin; set => SetField(ref _bpin, value); }
    
    private decimal _totalPoints;
    public decimal TotalPoints { get => _totalPoints; set => SetField(ref _totalPoints, value); }
    
    private decimal _balance;
    public decimal Balance { get => _balance; set => SetField(ref _balance, value); }
    
    private decimal _totalSpent;
    public decimal TotalSpent { get => _totalSpent; set => SetField(ref _totalSpent, value); }
    
    private string _status = string.Empty;
    public string Status { get => _status; set => SetField(ref _status, value); }
    
    private string? _identity;
    public string? Identity { get => _identity; set => SetField(ref _identity, value); }
    
    private string? _address;
    public string? Address { get => _address; set => SetField(ref _address, value); }
    
    private string? _guidePin;
    public string? GuidePin { get => _guidePin; set => SetField(ref _guidePin, value); }
    
    private string? _guideName;
    public string? GuideName { get => _guideName; set => SetField(ref _guideName, value); }
    
    private SyncStatus _syncStatus;
    public SyncStatus SyncStatus { get => _syncStatus; set => SetField(ref _syncStatus, value); }
    
    private DateTime _lastModified;
    public DateTime LastModified { get => _lastModified; set => SetField(ref _lastModified, value); }

    private DateTime _createdAt;
    public DateTime CreatedAt { get => _createdAt; set => SetField(ref _createdAt, value); }
}
