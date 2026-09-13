using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WanKePos.Domain.Enums;
using WanKePos.Domain.Interfaces;

namespace WanKePos.Domain.Entities;

/// <summary>
/// 商品实体 (实现 INotifyPropertyChanged 确保 UI 绑定在属性变更时毫秒级自动响应刷新)
/// </summary>
public class Product : IAuditableEntity, INotifyPropertyChanged
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

    private string _barcode = string.Empty;
    public string Barcode { get => _barcode; set => SetField(ref _barcode, value); }

    private string _name = string.Empty;
    public string Name { get => _name; set => SetField(ref _name, value); }

    private string _productType = string.Empty;
    public string ProductType { get => _productType; set => SetField(ref _productType, value); }

    private decimal _stock;
    public decimal Stock { get => _stock; set => SetField(ref _stock, value); }

    private decimal _retailPrice;
    public decimal RetailPrice { get => _retailPrice; set => SetField(ref _retailPrice, value); }

    private decimal _costPrice;
    public decimal CostPrice { get => _costPrice; set => SetField(ref _costPrice, value); }

    private decimal? _memberPrice;
    public decimal? MemberPrice { get => _memberPrice; set => SetField(ref _memberPrice, value); }

    private string _saleMethod = string.Empty;
    public string SaleMethod { get => _saleMethod; set => SetField(ref _saleMethod, value); }

    private string? _systemCategory;
    public string? SystemCategory { get => _systemCategory; set => SetField(ref _systemCategory, value); }

    private string? _storeCategory;
    public string? StoreCategory { get => _storeCategory; set => SetField(ref _storeCategory, value); }

    private string? _articleNumber;
    public string? ArticleNumber { get => _articleNumber; set => SetField(ref _articleNumber, value); }

    private string? _brand;
    public string? Brand { get => _brand; set => SetField(ref _brand, value); }

    private string? _saleUnit;
    public string? SaleUnit { get => _saleUnit; set => SetField(ref _saleUnit, value); }

    private string? _specification;
    public string? Specification { get => _specification; set => SetField(ref _specification, value); }

    private string? _specUnit;
    public string? SpecUnit { get => _specUnit; set => SetField(ref _specUnit, value); }

    private bool _isPointsEligible;
    public bool IsPointsEligible { get => _isPointsEligible; set => SetField(ref _isPointsEligible, value); }

    private string? _shelfStatus;
    public string? ShelfStatus { get => _shelfStatus; set => SetField(ref _shelfStatus, value); }

    private string? _supplier;
    public string? Supplier { get => _supplier; set => SetField(ref _supplier, value); }

    private string? _imageUrl;
    public string? ImageUrl { get => _imageUrl; set => SetField(ref _imageUrl, value); }

    private SyncStatus _syncStatus;
    public SyncStatus SyncStatus { get => _syncStatus; set => SetField(ref _syncStatus, value); }

    private DateTime _lastModified;
    public DateTime LastModified { get => _lastModified; set => SetField(ref _lastModified, value); }

    private DateTime _createdAt;
    public DateTime CreatedAt { get => _createdAt; set => SetField(ref _createdAt, value); }

    public void CopyFrom(Product other)
    {
        Barcode = other.Barcode;
        Name = other.Name;
        ProductType = other.ProductType;
        Stock = other.Stock;
        RetailPrice = other.RetailPrice;
        CostPrice = other.CostPrice;
        MemberPrice = other.MemberPrice;
        SaleMethod = other.SaleMethod;
        SystemCategory = other.SystemCategory;
        StoreCategory = other.StoreCategory;
        ArticleNumber = other.ArticleNumber;
        Brand = other.Brand;
        SaleUnit = other.SaleUnit;
        Specification = other.Specification;
        SpecUnit = other.SpecUnit;
        IsPointsEligible = other.IsPointsEligible;
        ShelfStatus = other.ShelfStatus;
        Supplier = other.Supplier;
        ImageUrl = other.ImageUrl;
        SyncStatus = other.SyncStatus;
        LastModified = other.LastModified;
    }
}
