using System.ComponentModel;
using System.Runtime.CompilerServices;
using Bitacora.Models;
using Bitacora.Services;

namespace Bitacora.ViewModels;

public class AuthViewModel : ViewModelBase
{
    private Usuario? _currentUser;
    private bool _isAuthenticated;

    public Usuario? CurrentUser
    {
        get => _currentUser;
        set => SetProperty(ref _currentUser, value);
    }

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        set => SetProperty(ref _isAuthenticated, value);
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        try
        {
            var usuario = await DatabaseService.Instance.LoginAsync(email.Trim(), password.Trim());
            
            if (usuario != null)
            {
                CurrentUser = usuario;
                IsAuthenticated = true;
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en login: {ex.Message}");
            return false;
        }
    }

    public void Logout()
    {
        CurrentUser = null;
        IsAuthenticated = false;
    }
}

