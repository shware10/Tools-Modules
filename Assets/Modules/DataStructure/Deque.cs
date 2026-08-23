using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public class Deque<T>
{
    private T[] _array;
    
    private int _head;
    private int _tail;
    private int _size;
    
    public int Count => _size;
    
    public bool IsEmpty => _size == 0;
    
    public Deque(int capacity = 4)
    {
        _array = new T[capacity];
        
        _head = 0;
        _tail = 0;
        _size = 0;
    }
    
    private void CheckCapacity()
    {
        if(_size < _array.Length) return;
        
        Resize(_array.Length * 2);
    }
    
    private void Resize(int newCapacity)
    {
        T[] newArray = new T[newCapacity];
        
        for(int i = 0; i < _size; ++i)
        {
            newArray[i] = _array[(_head + i) % _array.Length];
        }
        
        _array = newArray;
        _head = 0;
        _tail = _size;
    }
    
    public void PushBack(T value)
    {
        CheckCapacity();
        
        _array[_tail] = value;
        
        _tail = (_tail + 1) % _array.Length;
        _size++;
    }
    
    public void PushFront(T value)
    {
        CheckCapacity();
        
        _head = (_head + _array.Length - 1) % _array.Length;
        
        _array[_head] = value;
        _size++;
    }
    
    
    public T PopFront()
    {
        if(_size == 0) throw new InvalidOperationException();
        
        T value = _array[_head];
        
        _array[_head] = default;
        
        _head = (_head + 1) % _array.Length;
        
        _size--;
        
        return value;
    }
    
    public T PopBack()
    {
        if(_size == 0) throw new InvalidOperationException();
        
        _tail = (_tail - 1 + _array.Length) % _array.Length;
        
        T value = _array[_tail];
        
        _array[_tail] = default;
        
        _size--;
        
        return value;
    }
    
    public T Front
    {
        get
        {
            if(_size == 0) throw new InvalidOperationException();
            
            return _array[_head];
        }
    }
    
    public T Back
    {
        get
        {
            if(_size == 0) throw new InvalidOperationException();
            
            int index = (_tail - 1 + _array.Length) % _array.Length;
            
            return _array[index];
        }
    }
    
    public void Clear()
    {
        // 레퍼런스 타입이면 배열의 각 원소를 default(T)로 초기화합니다.
        if(RuntimeHelpers.IsReferenceOrContainsReferences<T>()) 
        {
            Array.Clear(_array, 0, _size);
        }
        
        _head = 0;
        _tail = 0;
        _size = 0;
    }
    
    public bool Contains(T value)
    {
        var target = EqualityComparer<T>.Default;
        
        for(int i = 0; i < _size; ++i)
        {
            int index = (_head + i) % _array.Length;
            
            if(target.Equals(_array[index], value)) return true;
        }
        
        return false;
    }
    
    public void CopyTo(T[] array, int arrayIndex)
    {
        if(array == null) throw new ArgumentNullException();
        
        if(arrayIndex < 0) throw new ArgumentOutOfRangeException();
        
        if(array.Length - arrayIndex < _size) throw new ArgumentException();
        
        for(int i = 0; i < _size; ++i)
        {
            array[arrayIndex + i] = _array[(_head + i) % _array.Length];
        }
    }
    
    public T[] ToArray()
    {
        T[] result = new T[_size];
        
        CopyTo(result, 0 );
        
        return result;
    }
}
